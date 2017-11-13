using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;

namespace NBitcoin
{
    /// <summary>
    /// Nodes collect new transactions into a block, hash them into a hash tree,
    /// and scan through nonce values to make the block's hash satisfy proof-of-work
    /// requirements.  When they solve the proof-of-work, they broadcast the block
    /// to everyone and the block is added to the block chain.  The first transaction
    /// in the block is a special one that creates a new coin owned by the creator
    /// of the block.
    /// </summary>
    public class BlockHeader : IBitcoinSerializable
    {
        internal const int Size = 80;

        /// <summary>Current header version.</summary>
        public const int CurrentVersion = 7;

        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        private uint256 hashPrevBlock;
        public uint256 HashPrevBlock { get { return this.hashPrevBlock; } set { this.hashPrevBlock = value; } }

        private uint time;
        public uint Time { get { return this.time; } set { this.time = value; } }

        private uint bits;
        public Target Bits { get { return this.bits; } set { this.bits = value; } }

        private int version;
        public int Version { get { return this.version; } set { this.version = value; } }

        private uint nonce;
        public uint Nonce { get { return this.nonce; } set { this.nonce = value; } }

        private uint256 hashMerkleRoot;
        public uint256 HashMerkleRoot { get { return this.hashMerkleRoot; } set { this.hashMerkleRoot = value; } }

        public bool IsNull { get { return (this.bits == 0); } }

        private uint256[] hashes;

        public DateTimeOffset BlockTime
        {
            get
            {
                return Utils.UnixTimeToDateTime(this.time);
            }
            set
            {
                this.time = Utils.DateTimeToUnixTime(value);
            }
        }

        public BlockHeader()
        {
            this.SetNull();
        }

        public BlockHeader(string hex)
            : this(Encoders.Hex.DecodeData(hex))
        {
        }

        public BlockHeader(byte[] bytes)
        {
            this.ReadWrite(bytes);
        }

        public static BlockHeader Parse(string hex)
        {
            return new BlockHeader(Encoders.Hex.DecodeData(hex));
        }

        internal void SetNull()
        {
            this.version = CurrentVersion;
            this.hashPrevBlock = 0;
            this.hashMerkleRoot = 0;
            this.time = 0;
            this.bits = 0;
            this.nonce = 0;
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.hashPrevBlock);
            stream.ReadWrite(ref this.hashMerkleRoot);
            stream.ReadWrite(ref this.time);
            stream.ReadWrite(ref this.bits);
            stream.ReadWrite(ref this.nonce);
        }

        #endregion

        public uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] hashes = this.hashes;

            if (hashes != null)
                hash = hashes[0];

            if (hash != null)
                return hash;

            if (Block.BlockSignature)
            {
                if (this.version > 6)
                    hash = Hashes.Hash256(this.ToBytes());
                else
                    hash = this.GetPoWHash();
            }
            else
            {
                using (HashStream hs = new HashStream())
                {
                    this.ReadWrite(new BitcoinStream(hs, true));
                    hash = hs.GetHash();
                }
            }

            hashes = this.hashes;
            if (hashes != null)
            {
                hashes[0] = hash;
            }

            return hash;
        }

        public uint256 GetPoWHash()
        {
            return HashX13.Instance.Hash(this.ToBytes());
        }

        /// <summary>
        /// If called, <see cref="GetHash"/> becomes cached, only use if you believe the instance will
        /// not be modified after calculation. Calling it a second type invalidate the cache.
        /// </summary>
        public void CacheHashes()
        {
            this.hashes = new uint256[1];
        }

        public bool CheckProofOfWork()
        {
            return this.CheckProofOfWork(null);
        }

        public bool CheckProofOfWork(Consensus consensus)
        {
            consensus = consensus ?? Consensus.Main;
            BigInteger bits = this.Bits.ToBigInteger();
            if ((bits.CompareTo(BigInteger.Zero) <= 0) || (bits.CompareTo(Pow256) >= 0))
                return false;

            // Check proof of work matches claimed amount.
            if (Block.BlockSignature) // Note this can only be called on a POW block.
                return this.GetPoWHash() <= this.Bits.ToUInt256();

            return consensus.GetPoWHash(this) <= this.Bits.ToUInt256();
        }

        public override string ToString()
        {
            return GetHash().ToString();
        }

        /// <summary>
        /// Set time to consensus acceptable value.
        /// </summary>
        /// <param name="now">The expected date.</param>
        /// <param name="consensus">Consensus.</param>
        /// <param name="prev">Previous block.</param>
        public void UpdateTime(DateTimeOffset now, Consensus consensus, ChainedBlock prev)
        {
            DateTimeOffset nOldTime = this.BlockTime;
            DateTimeOffset mtp = prev.GetMedianTimePast() + TimeSpan.FromSeconds(1);
            DateTimeOffset nNewTime = mtp > now ? mtp : now;

            if (nOldTime < nNewTime)
                this.BlockTime = nNewTime;

            // Updating time can change work required on testnet.
            if (consensus.PowAllowMinDifficultyBlocks)
                this.Bits = this.GetWorkRequired(consensus, prev);
        }

        /// <summary>
        /// Set time to consensus acceptable value.
        /// </summary>
        /// <param name="now">The expected date.</param>
        /// <param name="network">Network.</param>
        /// <param name="prev">Previous block.</param>
        public void UpdateTime(DateTimeOffset now, Network network, ChainedBlock prev)
        {
            this.UpdateTime(now, network.Consensus, prev);
        }

        public Target GetWorkRequired(Network network, ChainedBlock prev)
        {
            return this.GetWorkRequired(network.Consensus, prev);
        }

        public Target GetWorkRequired(Consensus consensus, ChainedBlock prev)
        {
            return new ChainedBlock(this, null, prev).GetWorkRequired(consensus);
        }
    }

    public partial class Block : IBitcoinSerializable
    {
        public const uint MaxBlockSize = 1000 * 1000;

        private BlockHeader header = new BlockHeader();

        // network and disk
        private List<Transaction> transactions = new List<Transaction>();
        public List<Transaction> Transactions { get { return this.transactions; } set { this.transactions = value; } }

        public MerkleNode GetMerkleRoot()
        {
            return MerkleNode.GetRoot(this.Transactions.Select(t => t.GetHash()));
        }

        public Block()
        {
            this.SetNull();
        }

        public Block(BlockHeader blockHeader)
        {
            this.SetNull();
            this.header = blockHeader;
        }

        public Block(byte[] bytes)
        {
            this.ReadWrite(bytes);
        }


        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.header);
            stream.ReadWrite(ref this.transactions);
            if (Block.BlockSignature)
                stream.ReadWrite(ref this.blockSignature);
        }

        public bool HeaderOnly
        {
            get
            {
                return (this.transactions == null) || (this.transactions.Count == 0);
            }
        }

        void SetNull()
        {
            this.header.SetNull();
            this.transactions.Clear();
        }

        public BlockHeader Header
        {
            get
            {
                return this.header;
            }
        }
        public uint256 GetHash()
        {
            // Block's hash is his header's hash.
            return this.header.GetHash();
        }

        public void ReadWrite(byte[] array, int startIndex)
        {
            var ms = new MemoryStream(array);
            ms.Position += startIndex;
            BitcoinStream bitStream = new BitcoinStream(ms, false);
            this.ReadWrite(bitStream);
        }

        public Transaction AddTransaction(Transaction tx)
        {
            this.Transactions.Add(tx);
            return tx;
        }

        /// <summary>
        /// Create a block with the specified option only. (useful for stripping data from a block).
        /// </summary>
        /// <param name="options">Options to keep.</param>
        /// <returns>A new block with only the options wanted.</returns>
        public Block WithOptions(TransactionOptions options)
        {
            if (this.Transactions.Count == 0)
                return this;

            if ((options == TransactionOptions.Witness) && this.Transactions[0].HasWitness)
                return this;

            if ((options == TransactionOptions.None) && !this.Transactions[0].HasWitness)
                return this;

            var instance = new Block();
            var ms = new MemoryStream();
            var bms = new BitcoinStream(ms, true)
            {
                TransactionOptions = options
            };

            this.ReadWrite(bms);
            ms.Position = 0;
            bms = new BitcoinStream(ms, false)
            {
                TransactionOptions = options
            };

            instance.ReadWrite(bms);
            return instance;
        }

        public void UpdateMerkleRoot()
        {
            this.Header.HashMerkleRoot = GetMerkleRoot().Hash;
        }

        /// <summary>
        /// Check proof of work and merkle root
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            return this.Check(null);
        }

        /// <summary>
        /// Check proof of work and merkle root
        /// </summary>
        /// <param name="consensus"></param>
        /// <returns></returns>
        public bool Check(Consensus consensus)
        {
            if (Block.BlockSignature)
                return BlockStake.Check(this);

            return this.CheckMerkleRoot() && this.Header.CheckProofOfWork(consensus);
        }

        public bool CheckProofOfWork()
        {
            return this.CheckProofOfWork(null);
        }

        public bool CheckProofOfWork(Consensus consensus)
        {
            return this.Header.CheckProofOfWork(consensus);
        }

        public bool CheckMerkleRoot()
        {
            return this.Header.HashMerkleRoot == GetMerkleRoot().Hash;
        }

        public Block CreateNextBlockWithCoinbase(BitcoinAddress address, int height)
        {
            return this.CreateNextBlockWithCoinbase(address, height, DateTimeOffset.UtcNow);
        }

        public Block CreateNextBlockWithCoinbase(BitcoinAddress address, int height, DateTimeOffset now)
        {
            if (address == null)
                throw new ArgumentNullException("address");

            Block block = new Block();
            block.Header.Nonce = RandomUtils.GetUInt32();
            block.Header.HashPrevBlock = this.GetHash();
            block.Header.BlockTime = now;

            Transaction tx = block.AddTransaction(new Transaction());
            tx.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(RandomUtils.GetBytes(30)))
            });

            tx.Outputs.Add(new TxOut(address.Network.GetReward(height), address)
            {
                Value = address.Network.GetReward(height)
            });

            return block;
        }

        public Block CreateNextBlockWithCoinbase(PubKey pubkey, Money value)
        {
            return this.CreateNextBlockWithCoinbase(pubkey, value, DateTimeOffset.UtcNow);
        }

        public Block CreateNextBlockWithCoinbase(PubKey pubkey, Money value, DateTimeOffset now)
        {
            Block block = new Block();
            block.Header.Nonce = RandomUtils.GetUInt32();
            block.Header.HashPrevBlock = this.GetHash();
            block.Header.BlockTime = now;
            Transaction tx = block.AddTransaction(new Transaction());

            tx.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(RandomUtils.GetBytes(30)))
            });

            tx.Outputs.Add(new TxOut()
            {
                Value = value,
                ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(pubkey)
            });

            return block;
        }

        public static Block ParseJson(string json)
        {
            var formatter = new BlockExplorerFormatter();
            JObject block = JObject.Parse(json);
            JArray txs = (JArray)block["tx"];
            Block blk = new Block();
            blk.Header.Bits = new Target((uint)block["bits"]);
            blk.Header.BlockTime = Utils.UnixTimeToDateTime((uint)block["time"]);
            blk.Header.Nonce = (uint)block["nonce"];
            blk.Header.Version = (int)block["ver"];
            blk.Header.HashPrevBlock = uint256.Parse((string)block["prev_block"]);
            blk.Header.HashMerkleRoot = uint256.Parse((string)block["mrkl_root"]);

            foreach (JToken tx in txs)
            {
                blk.AddTransaction(formatter.Parse((JObject)tx));
            }

            return blk;
        }

        public static Block Parse(string hex)
        {
            return new Block(Encoders.Hex.DecodeData(hex));
        }

        public MerkleBlock Filter(params uint256[] txIds)
        {
            return new MerkleBlock(this, txIds);
        }

        public MerkleBlock Filter(BloomFilter filter)
        {
            return new MerkleBlock(this, filter);
        }
    }
}