using System;
using Nethereum.RLP;
using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts.Hashing;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// Adapted from EthereumJ. Magic data structure where entire state
    /// can be represented by a 32-byte root. 
    /// Read more here: https://github.com/ethereum/wiki/wiki/Patricia-Tree
    /// </summary>
    public class PatriciaTrie : ITrie<byte[]>
    {
        public enum NodeType
        {
            BranchNode,
            KVNodeValue,
            KVNodeNode
        }

        private static readonly object NullNode = new object();

        public sealed class Node
        {
            public byte[] Hash { get; private set; }
            private byte[] rlp = null;
            private RLPLList parsedRlp = null;
            public bool Dirty { get; private set; } = false;

            private object[] children;
            
            // purely used for reference to cache
            private PatriciaTrie trie;

            public NodeType NodeType
            {
                get
                {
                    Parse();
                    return this.children.Length == 17 ? NodeType.BranchNode : (this.children[1] is Node ? NodeType.KVNodeNode : NodeType.KVNodeValue);
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
            public Node(TrieKey key, object valueOrNode, PatriciaTrie trie) : this(new object[] { key, valueOrNode}, trie)
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

            public Node(RLPLList parsedRlp, PatriciaTrie trie)
            {
                this.parsedRlp = parsedRlp;
                this.rlp = parsedRlp.GetEncoded();
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
                if (!ResolveCheck())
                    throw new Exception("Invalid trie state, can't resolve hash.");
            }

            public byte[] Encode()
            {
                return Encode(1, true);
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
                            Node child = BranchNodeGetChild(i);
                            encoded[i] = child == null ? HashHelper.EmptyElementRlp : child.Encode(depth + 1, false);
                        }
                        byte[] value = BranchNodeGetValue();
                        encoded[16] = RLP.EncodeElement(value);
                        ret = RLP.EncodeList(encoded);
                    }
                    else if (type == NodeType.KVNodeNode)
                    {
                        ret = RLP.EncodeList(RLP.EncodeElement(KvNodeGetKey().ToPacked()), KvNodeGetChildNode().Encode(depth + 1, false));
                    }
                    else
                    {
                        byte[] value = KvNodeGetValue();
                        ret = RLP.EncodeList(RLP.EncodeElement(KvNodeGetKey().ToPacked()),
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
                Resolve();

                RLPLList list = this.parsedRlp == null ? RLPLList.DecodeLazyList(this.rlp) : this.parsedRlp;

                if (list.Size() == 2)
                {
                    this.children = new object[2];
                    TrieKey key = TrieKey.FromPacked(list.GetBytes(0));
                    this.children[0] = key;
                    if (key.IsTerminal)
                    {
                        this.children[1] = list.GetBytes(1);
                    }
                    else
                    {
                        this.children[1] = list.IsList(1) ? new Node(list.GetList(1), this.trie) : new Node(list.GetBytes(1), this.trie);
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
                Parse();
                object n = this.children[hex];
                if (n == null && this.parsedRlp != null)
                {
                    if (this.parsedRlp.IsList(hex))
                    {
                        n = new Node(this.parsedRlp.GetList(hex), this.trie);
                    }
                    else
                    {
                        byte[] bytes = this.parsedRlp.GetBytes(hex);
                        if (bytes.Length == 0)
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
                Parse();
                this.children[hex] = node == null ? NullNode : node;
                this.Dirty = true;
                return this;
            }

            public byte[] BranchNodeGetValue()
            {
                Parse();
                object n = this.children[16];
                if (n == null && this.parsedRlp != null)
                {
                    byte[] bytes = this.parsedRlp.GetBytes(16);
                    if (bytes.Length == 0)
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
                Parse();
                this.children[16] = val == null ? NullNode : val;
                this.Dirty = true;
                return this;
            }

            public int BranchNodeCompactIdx()
            {
                Parse();
                int cnt = 0;
                int idx = -1;
                for (int i = 0; i < 16; i++)
                {
                    if (BranchNodeGetChild(i) != null)
                    {
                        cnt++;
                        idx = i;
                        if (cnt > 1) return -1;
                    }
                }
                return cnt > 0 ? idx : (BranchNodeGetValue() == null ? -1 : 16);
            }

            public bool BranchNodeCanCompact()
            {
                Parse();
                int cnt = 0;
                for (int i = 0; i < 16; i++)
                {
                    cnt += BranchNodeGetChild(i) == null ? 0 : 1;
                    if (cnt > 1) return false;
                }
                return cnt == 0 || BranchNodeGetValue() == null;
            }

            public TrieKey KvNodeGetKey()
            {
                Parse();
                return (TrieKey)this.children[0];
            }

            public Node KvNodeGetChildNode()
            {
                Parse();
                return (Node)this.children[1];
            }

            public byte[] KvNodeGetValue()
            {
                Parse();
                return (byte[])this.children[1];
            }

            public Node KvNodeSetValue(byte[] value)
            {
                Parse();
                this.children[1] = value;
                this.Dirty = true;
                return this;
            }

            public object KvNodeGetValueOrNode()
            {
                Parse();
                return this.children[1];
            }

            public Node KvNodeSetValueOrNode(object valueOrNode)
            {
                Parse();
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

        private ISource<byte[], byte[]> cache;
        private Node root;

        public PatriciaTrie() : this((byte[]) null) { }

        public PatriciaTrie(byte[] root) : this(new MemoryDictionarySource(), root) { }

        public PatriciaTrie(ISource<byte[],byte[]> cache) : this(cache, null) { }
        
        public PatriciaTrie(ISource<byte[],byte[]> cache, byte[] root)
        {
            this.cache = cache;
            SetRoot(root);
        }

        public void SetRoot(byte[] root)
        {
            if (root != null && !new ByteArrayComparer().Equals(root, HashHelper.EmptyTrieHash))
            {
                this.root = new Node(root, this);
            }
            else
            {
                this.root = null;
            }
        }

        public ISource<byte[],byte[]> GetCache()
        {
            return this.cache;
        }
        
        private bool HasRoot()
        {
            return this.root != null && this.root.ResolveCheck();
        }

        private byte[] GetHash(byte[] hash)
        {
            return this.cache.Get(hash);
        }
        private void AddHash(byte[] hash, byte[] ret)
        {
            this.cache.Put(hash, ret);
        }
        private void DeleteHash(byte[] hash)
        {
            this.cache.Delete(hash);
        }

        public byte[] Get(byte[] key)
        {
            if (!HasRoot())
                return null;
            TrieKey k = TrieKey.FromNormal(key);
            return Get(this.root, k);
        }

        private byte[] Get(Node n, TrieKey k)
        {
            if (n == null)
                return null;

            NodeType type = n.NodeType;

            if (type == NodeType.BranchNode)
            {
                if (k.IsEmpty)
                    return n.BranchNodeGetValue();

                Node childNode = n.BranchNodeGetChild(k.GetHex(0));
                return Get(childNode, k.Shift(1));
            }
            else
            {
                TrieKey k1 = k.MatchAndShift(n.KvNodeGetKey());
                if (k1 == null) return null;
                if (type == NodeType.KVNodeValue)
                {
                    return k1.IsEmpty ? n.KvNodeGetValue() : null;
                }
                else
                {
                    return Get(n.KvNodeGetChildNode(), k1);
                }
            }
        }

        public void Put(byte[] key, byte[] value)
        {
            TrieKey k = TrieKey.FromNormal(key);
            if (this.root == null)
            {
                if (value != null && value.Length > 0)
                {
                    this.root = new Node(k, value, this);
                }
            }
            else
            {
                if (value == null || value.Length == 0)
                {
                    this.root = Delete(this.root, k);
                }
                else
                {
                    this.root = Insert(this.root, k, value);
                }
            }
        }

        private Node Insert(Node n, TrieKey k, object nodeOrValue)
        {
            NodeType type = n.NodeType;
            if (type == NodeType.BranchNode)
            {
                if (k.IsEmpty) return n.BranchNodeSetValue((byte[])nodeOrValue);
                Node childNode = n.BranchNodeGetChild(k.GetHex(0));
                if (childNode != null)
                {
                    return n.BranchNodeSetChild(k.GetHex(0), Insert(childNode, k.Shift(1), nodeOrValue));
                }
                else
                {
                    TrieKey childKey = k.Shift(1);
                    Node newChildNode;
                    if (!childKey.IsEmpty)
                    {
                        newChildNode = new Node(childKey, nodeOrValue, this);
                    }
                    else
                    {
                        newChildNode = nodeOrValue is Node ?
                            (Node)nodeOrValue : new Node(childKey, nodeOrValue, this);
                    }
                    return n.BranchNodeSetChild(k.GetHex(0), newChildNode);
                }
            }
            else
            {
                TrieKey commonPrefix = k.GetCommonPrefix(n.KvNodeGetKey());
                if (commonPrefix.IsEmpty)
                {
                    Node newBranchNode = new Node(this);
                    Insert(newBranchNode, n.KvNodeGetKey(), n.KvNodeGetValueOrNode());
                    Insert(newBranchNode, k, nodeOrValue);
                    n.Dispose();
                    return newBranchNode;
                }
                else if (commonPrefix.Equals(k))
                {
                    return n.KvNodeSetValueOrNode(nodeOrValue);
                }
                else if (commonPrefix.Equals(n.KvNodeGetKey()))
                {
                    Insert(n.KvNodeGetChildNode(), k.Shift(commonPrefix.Length), nodeOrValue);
                    return n.Invalidate();
                }
                else
                {
                    Node newBranchNode = new Node(this);
                    Node newKvNode = new Node(commonPrefix, newBranchNode, this);
                    // TODO can be optimized
                    Insert(newKvNode, n.KvNodeGetKey(), n.KvNodeGetValueOrNode());
                    Insert(newKvNode, k, nodeOrValue);
                    n.Dispose();
                    return newKvNode;
                }
            }
        }

        public void Delete(byte[] key)
        {
            TrieKey k = TrieKey.FromNormal(key);
            if (this.root != null)
            {
                this.root = Delete(this.root, k);
            }
        }

        private Node Delete(Node n, TrieKey k)
        {
            NodeType type = n.NodeType;
            Node newKvNode;
            if (type == NodeType.BranchNode)
            {
                if (k.IsEmpty)
                {
                    n.BranchNodeSetValue(null);
                }
                else
                {
                    int idx = k.GetHex(0);
                    Node child = n.BranchNodeGetChild(idx);
                    if (child == null) return n; // no key found

                    Node newNode = Delete(child, k.Shift(1));
                    n.BranchNodeSetChild(idx, newNode);
                    if (newNode != null) return n; // newNode != null thus number of children didn't decrease
                }

                // child node or value was deleted and the branch node may need to be compacted
                int compactIdx = n.BranchNodeCompactIdx();
                if (compactIdx < 0) return n; // no compaction is required

                // only value or a single child left - compact branch node to kvNode
                n.Dispose();
                if (compactIdx == 16)
                { // only value left
                    return new Node(TrieKey.Empty(true), n.BranchNodeGetValue(), this);
                }
                else
                { // only single child left
                    newKvNode = new Node(TrieKey.SingleHex(compactIdx), n.BranchNodeGetChild(compactIdx), this);
                }
            }
            else
            { // n - kvNode
                TrieKey k1 = k.MatchAndShift(n.KvNodeGetKey());
                if (k1 == null)
                {
                    // no key found
                    return n;
                }
                else if (type == NodeType.KVNodeValue)
                {
                    if (k1.IsEmpty)
                    {
                        // delete this kvNode
                        n.Dispose();
                        return null;
                    }
                    else
                    {
                        // else no key found
                        return n;
                    }
                }
                else
                {
                    Node newChild = Delete(n.KvNodeGetChildNode(), k1);
                    if (newChild == null) throw new Exception("Shouldn't happen");
                    newKvNode = n.KvNodeSetValueOrNode(newChild);
                }
            }

            // if we get here a new kvNode was created, now need to check
            // if it should be compacted with child kvNode
            Node nChild = newKvNode.KvNodeGetChildNode();
            if (nChild.NodeType != NodeType.BranchNode)
            {
                // two kvNodes should be compacted into a single one
                TrieKey newKey = newKvNode.KvNodeGetKey().Concat(nChild.KvNodeGetKey());
                Node newNode = new Node(newKey, nChild.KvNodeGetValueOrNode(), this);
                nChild.Dispose();
                return newNode;
            }
            else
            {
                // no compaction needed
                return newKvNode;
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Flush()
        {
            if (this.root != null && this.root.Dirty)
            {
                // persist all dirty nodes to underlying Source
                Encode();
                // release all Trie Node instances for GC
                this.root = new Node(this.root.Hash, this);
                return true;
            }
            else
            {
                return false;
            }
        }

        public byte[] GetRootHash()
        {
            Encode();
            return this.root != null ? this.root.Hash : HashHelper.EmptyTrieHash;
        }

        private void Encode()
        {
            if (this.root != null)
                this.root.Encode();
        }
    }
}
