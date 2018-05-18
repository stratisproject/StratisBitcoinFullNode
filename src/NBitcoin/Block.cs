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
        public virtual int CurrentVersion => 3;

        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        private uint256 hashPrevBlock;
        public uint256 HashPrevBlock { get { return this.hashPrevBlock; } set { this.hashPrevBlock = value; } }

        private uint time;
        public uint Time { get { return this.time; } set { this.time = value; } }

        private uint bits;
        public Target Bits { get { return this.bits; } set { this.bits = value; } }

        protected int version;

        public int Version { get { return this.version; } set { this.version = value; } }

        private uint nonce;
        public uint Nonce { get { return this.nonce; } set { this.nonce = value; } }

        private uint256 hashMerkleRoot;
        public uint256 HashMerkleRoot { get { return this.hashMerkleRoot; } set { this.hashMerkleRoot = value; } }

        public bool IsNull { get { return (this.bits == 0); } }

        protected uint256[] hashes;

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

        public static BlockHeader Load(byte[] hex, Network network)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            if (network == null)
                throw new ArgumentNullException(nameof(network));

            BlockHeader blockHeader = network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.ReadWrite(hex, network: network);

            return blockHeader;
        }

        internal void SetNull()
        {
            this.version = this.CurrentVersion;
            this.hashPrevBlock = 0;
            this.hashMerkleRoot = 0;
            this.time = 0;
            this.bits = 0;
            this.nonce = 0;
        }

        #region IBitcoinSerializable Members

        public virtual void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.hashPrevBlock);
            stream.ReadWrite(ref this.hashMerkleRoot);
            stream.ReadWrite(ref this.time);
            stream.ReadWrite(ref this.bits);
            stream.ReadWrite(ref this.nonce);
        }

        #endregion

        public virtual uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] hashes = this.hashes;

            if (hashes != null)
                hash = hashes[0];

            if (hash != null)
                return hash;

            using (HashStream hs = new HashStream())
            {
                this.ReadWrite(new BitcoinStream(hs, true));
                hash = hs.GetHash();
            }

            hashes = this.hashes;
            if (hashes != null)
            {
                hashes[0] = hash;
            }

            return hash;
        }

        public virtual uint256 GetPoWHash()
        {
            return this.GetHash();
        }

        /// <summary>
        /// Precompute the block header hash so that later calls to <see cref="GetHash()"/> will returns the precomputed hash.
        /// </summary>
        /// <param name="invalidateExisting">If true, the previous precomputed hash is thrown away, else it is reused.</param>
        /// <param name="lazily">If <c>true</c>, the hash will be calculated and cached at the first call to GetHash(), else it will be immediately.</param>
        public void PrecomputeHash(bool invalidateExisting = false, bool lazily = false)
        {
            if (this.hashes == null || invalidateExisting)
                this.hashes = new uint256[1];

            if (!lazily && this.hashes[0] == null)
                this.hashes[0] = this.GetHash();
        }

        public bool CheckProofOfWork(Consensus consensus)
        {
            BigInteger bits = this.Bits.ToBigInteger();
            if ((bits.CompareTo(BigInteger.Zero) <= 0) || (bits.CompareTo(Pow256) >= 0))
                return false;

            return this.GetPoWHash() <= this.Bits.ToUInt256();
        }

        public override string ToString()
        {
            return this.GetHash().ToString();
        }

        /// <summary>
        /// Set time to consensus acceptable value.
        /// </summary>
        /// <param name="now">The expected date.</param>
        /// <param name="consensus">Consensus.</param>
        /// <param name="prev">Previous block.</param>
        public void UpdateTime(DateTimeOffset now, Consensus consensus, ChainedHeader prev)
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
        public void UpdateTime(DateTimeOffset now, Network network, ChainedHeader prev)
        {
            this.UpdateTime(now, network.Consensus, prev);
        }

        public Target GetWorkRequired(Network network, ChainedHeader prev)
        {
            return this.GetWorkRequired(network.Consensus, prev);
        }

        public Target GetWorkRequired(Consensus consensus, ChainedHeader prev)
        {
            return new ChainedHeader(this, this.GetHash(), prev).GetWorkRequired(consensus);
        }
    }

    public partial class Block : IBitcoinSerializable
    {
        public const uint MaxBlockSize = 1000 * 1000;

        private BlockHeader header;

        // network and disk
        private List<Transaction> transactions = new List<Transaction>();
        public List<Transaction> Transactions { get { return this.transactions; } set { this.transactions = value; } }

        public MerkleNode GetMerkleRoot()
        {
            return MerkleNode.GetRoot(this.Transactions.Select(t => t.GetHash()));
        }

        [Obsolete("Should use Block.Load outside of ConsensusFactories")]
        public Block()
        {
            this.header = new BlockHeader();
            this.SetNull();
        }

        [Obsolete("Should use Block.Load outside of ConsensusFactories")]
        internal Block(BlockHeader blockHeader)
        {
            this.header = new BlockHeader();
            this.SetNull();
            this.header = blockHeader;
        }

        [Obsolete("Should use Block.Load outside of ConsensusFactories")]
        internal Block(byte[] bytes, ConsensusFactory consensusFactory)
        {
            BitcoinStream stream = new BitcoinStream(bytes)
            {
                ConsensusFactory = consensusFactory
            };

            this.ReadWrite(stream);
        }

        [Obsolete("Should use Block.Load outside of ConsensusFactories")]
        internal Block(byte[] bytes) : this(bytes, Network.Main.Consensus.ConsensusFactory)
        {
        }

        public virtual void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.header);
            stream.ReadWrite(ref this.transactions);
        }

        public bool HeaderOnly
        {
            get
            {
                return (this.transactions == null) || (this.transactions.Count == 0);
            }
        }

        private void SetNull()
        {
            this.header.SetNull();
            this.transactions.Clear();
        }

        public BlockHeader Header => this.header;

        public uint256 GetHash()
        {
            // Block's hash is his header's hash.
            return this.header.GetHash();
        }

        public Transaction AddTransaction(Transaction tx)
        {
            this.Transactions.Add(tx);
            return tx;
        }

        /// <summary>
        /// Create a block with the specified option only. (useful for stripping data from a block).
        /// </summary>
        /// <param name="consensusFactory">The network consensus factory.</param>
        /// <param name="options">Options to keep.</param>
        /// <returns>A new block with only the options wanted.</returns>
        public Block WithOptions(ConsensusFactory consensusFactory, TransactionOptions options)
        {
            if (this.Transactions.Count == 0)
                return this;

            if ((options == TransactionOptions.Witness) && this.Transactions[0].HasWitness)
                return this;

            if ((options == TransactionOptions.None) && !this.Transactions[0].HasWitness)
                return this;

            Block instance = consensusFactory.CreateBlock();
            var ms = new MemoryStream();
            var bms = new BitcoinStream(ms, true)
            {
                TransactionOptions = options,
                ConsensusFactory = consensusFactory
            };

            this.ReadWrite(bms);
            ms.Position = 0;
            bms = new BitcoinStream(ms, false)
            {
                TransactionOptions = options,
                ConsensusFactory = consensusFactory
            };

            instance.ReadWrite(bms);
            return instance;
        }

        public void UpdateMerkleRoot()
        {
            this.Header.HashMerkleRoot = GetMerkleRoot().Hash;
        }

        public bool CheckProofOfWork(Consensus consensus)
        {
            return this.Header.CheckProofOfWork(consensus);
        }

        public bool CheckMerkleRoot()
        {
            return this.Header.HashMerkleRoot == GetMerkleRoot().Hash;
        }

        public static Block ParseJson(Network network, string json)
        {
            var formatter = new BlockExplorerFormatter();
            JObject block = JObject.Parse(json);
            JArray txs = (JArray)block["tx"];
            Block blk = network.Consensus.ConsensusFactory.CreateBlock();
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

        public static Block Parse(string hex, Network network)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentNullException(nameof(hex));

            if (network == null)
                throw new ArgumentNullException(nameof(network));

            Block block = network.Consensus.ConsensusFactory.CreateBlock();
            block.ReadWrite(Encoders.Hex.DecodeData(hex), network: network);

            return block;
        }

        public static Block Load(byte[] hex, Network network)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            if (network == null)
                throw new ArgumentNullException(nameof(network));

            Block block = network.Consensus.ConsensusFactory.CreateBlock();
            block.ReadWrite(hex, network: network);

            return block;
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