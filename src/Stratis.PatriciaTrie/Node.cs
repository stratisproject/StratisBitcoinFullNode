using System;
using Nethereum.RLP;

namespace Stratis.Patricia
{
    internal sealed class Node
    {
        private static readonly object NullNode = new object();

        public byte[] Hash { get; private set; }
        private byte[] rlp = null;
        private RLPCollection parsedRlp = null;
        public bool Dirty { get; private set; } = false;

        private object[] children;

        // purely used for reference to cache
        private readonly PatriciaTrie trie;

        public NodeType NodeType
        {
            get
            {
                this.Parse();
                return this.children.Length == 17 ? NodeType.BranchNode : (this.children[1] is Node ? NodeType.KeyValueNodeNode : NodeType.KeyValueNodeValue);
            }
        }

        // new empty BranchNode
        public Node(PatriciaTrie trie)
        {
            this.children = new object[17];
            this.Dirty = true;
            this.trie = trie;
        }

        // new KVNode with key and (value or node)
        public Node(Key key, object valueOrNode, PatriciaTrie trie) : this(new object[] { key, valueOrNode }, trie)
        {
            this.Dirty = true;
        }

        // new Node with hash or RLP
        public Node(byte[] hashOrRlp, PatriciaTrie trie)
        {
            if (hashOrRlp.Length == 32)
            {
                this.Hash = hashOrRlp;
            }
            else
            {
                this.rlp = hashOrRlp;
            }
            this.trie = trie;
        }

        public Node(RLPCollection parsedRlp, PatriciaTrie trie)
        {
            this.parsedRlp = parsedRlp;
            this.rlp = RLP.EncodeElement(parsedRlp.RLPData);
            this.trie = trie;
        }

        private Node(object[] children, PatriciaTrie trie)
        {
            this.children = children;
            this.trie = trie;
        }

        public bool ResolveCheck()
        {
            if (this.rlp != null || this.parsedRlp != null || this.Hash == null) return true;
            this.rlp = this.trie.GetHash(this.Hash);
            return this.rlp != null;
        }

        private void Resolve()
        {
            if (!this.ResolveCheck())
                throw new Exception("Invalid trie state, can't resolve hash.");
        }

        public byte[] Encode()
        {
            return this.Encode(1, true);
        }

        private byte[] Encode(int depth, bool forceHash)
        {
            if (!this.Dirty)
            {
                return this.Hash != null ? RLP.EncodeElement(this.Hash) : this.rlp;
            }
            else
            {
                NodeType type = this.NodeType;
                byte[] ret;
                if (type == NodeType.BranchNode)
                {
                    byte[][] encoded = new byte[17][];
                    for (int i = 0; i < 16; i++)
                    {
                        Node child = this.BranchNodeGetChild(i);
                        encoded[i] = child == null ? HashHelper.EmptyElementRlp : child.Encode(depth + 1, false);
                    }
                    byte[] value = this.BranchNodeGetValue();
                    encoded[16] = RLP.EncodeElement(value);
                    ret = RLP.EncodeList(encoded);
                }
                else if (type == NodeType.KeyValueNodeNode)
                {
                    ret = RLP.EncodeList(RLP.EncodeElement(this.KvNodeGetKey().ToPacked()), this.KvNodeGetChildNode().Encode(depth + 1, false));
                }
                else
                {
                    byte[] value = this.KvNodeGetValue();
                    ret = RLP.EncodeList(RLP.EncodeElement(this.KvNodeGetKey().ToPacked()),
                                    RLP.EncodeElement(value == null ? HashHelper.EmptyByteArray : value));
                }
                if (this.Hash != null)
                {
                    this.trie.DeleteHash(this.Hash);
                }
                this.Dirty = false;
                if (ret.Length < 32 && !forceHash)
                {
                    this.rlp = ret;
                    return ret;
                }
                else
                {
                    this.Hash = HashHelper.Keccak256(ret);
                    this.trie.AddHash(this.Hash, ret);
                    return RLP.EncodeElement(this.Hash);
                }
            }
        }

        private void Parse()
        {
            if (this.children != null) return;
            this.Resolve();

            RLPCollection list = (this.parsedRlp == null) ? RLP.Decode(this.rlp)[0] as RLPCollection : this.parsedRlp;

            if (list.Count == 2)
            {
                this.children = new object[2];
                Key key = Key.FromPacked(list[0].RLPData);
                this.children[0] = key;
                if (key.IsTerminal)
                {
                    this.children[1] = list[1].RLPData;
                }
                else
                {
                    this.children[1] = (list[1] is RLPCollection) ? new Node((RLPCollection)list[1], this.trie) : new Node(list[1].RLPData, this.trie);
                }
            }
            else
            {
                this.children = new object[17];
                this.parsedRlp = list;
            }
        }

        public Node BranchNodeGetChild(int hex)
        {
            this.Parse();
            object n = this.children[hex];
            if (n == null && this.parsedRlp != null)
            {
                if (this.parsedRlp[hex] is RLPCollection)
                {
                    n = new Node((RLPCollection)this.parsedRlp[hex], this.trie);
                }
                else
                {
                    byte[] bytes = this.parsedRlp[hex].RLPData;
                    if (bytes == null || bytes.Length == 0)
                    {
                        n = NullNode;
                    }
                    else
                    {
                        n = new Node(bytes, this.trie);
                    }
                }
                this.children[hex] = n;
            }
            return n == NullNode ? null : (Node)n;
        }

        public Node BranchNodeSetChild(int hex, Node node)
        {
            this.Parse();
            this.children[hex] = node == null ? NullNode : node;
            this.Dirty = true;
            return this;
        }

        public byte[] BranchNodeGetValue()
        {
            this.Parse();
            object n = this.children[16];
            if (n == null && this.parsedRlp != null)
            {
                byte[] bytes = this.parsedRlp[16].RLPData;
                if (bytes == null || bytes.Length == 0)
                {
                    n = NullNode;
                }
                else
                {
                    n = bytes;
                }
                this.children[16] = n;
            }
            return n == NullNode ? null : (byte[])n;
        }

        public Node BranchNodeSetValue(byte[] val)
        {
            this.Parse();
            this.children[16] = val == null ? NullNode : val;
            this.Dirty = true;
            return this;
        }

        public int BranchNodeCompactIdx()
        {
            this.Parse();
            int cnt = 0;
            int idx = -1;
            for (int i = 0; i < 16; i++)
            {
                if (this.BranchNodeGetChild(i) != null)
                {
                    cnt++;
                    idx = i;
                    if (cnt > 1) return -1;
                }
            }
            return cnt > 0 ? idx : (this.BranchNodeGetValue() == null ? -1 : 16);
        }

        public bool BranchNodeCanCompact()
        {
            this.Parse();
            int cnt = 0;
            for (int i = 0; i < 16; i++)
            {
                cnt += this.BranchNodeGetChild(i) == null ? 0 : 1;
                if (cnt > 1) return false;
            }
            return cnt == 0 || this.BranchNodeGetValue() == null;
        }

        public Key KvNodeGetKey()
        {
            this.Parse();
            return (Key)this.children[0];
        }

        public Node KvNodeGetChildNode()
        {
            this.Parse();
            return (Node)this.children[1];
        }

        public byte[] KvNodeGetValue()
        {
            this.Parse();
            return (byte[])this.children[1];
        }

        public Node KvNodeSetValue(byte[] value)
        {
            this.Parse();
            this.children[1] = value;
            this.Dirty = true;
            return this;
        }

        public object KvNodeGetValueOrNode()
        {
            this.Parse();
            return this.children[1];
        }

        public Node KvNodeSetValueOrNode(object valueOrNode)
        {
            this.Parse();
            this.children[1] = valueOrNode;
            this.Dirty = true;
            return this;
        }

        public void Dispose()
        {
            if (this.Hash != null)
            {
                this.trie.DeleteHash(this.Hash);
            }
        }

        public Node Invalidate()
        {
            this.Dirty = true;
            return this;
        }
    }

}
