using System;
using System.IO;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    [Flags]
    public enum BlockFlag //block index flags
    {
        BLOCK_PROOF_OF_STAKE = (1 << 0), // is proof-of-stake block
        BLOCK_STAKE_ENTROPY = (1 << 1), // entropy bit for stake modifier
        BLOCK_STAKE_MODIFIER = (1 << 2), // regenerated stake modifier
    };

    public class BlockStake : IBitcoinSerializable
    {
        public int Mint;

        public OutPoint PrevoutStake;

        public uint StakeTime;

        public ulong StakeModifier; // hash modifier for proof-of-stake

        public uint256 StakeModifierV2;

        private int flags;

        public uint256 HashProof;

        public BlockStake()
        {
        }

        public BlockFlag Flags
        {
            get
            {
                return (BlockFlag)this.flags;
            }
            set
            {
                this.flags = (int)value;
            }
        }

        public static bool IsProofOfStake(Block block)
        {
            return block.Transactions.Count > 1 && block.Transactions[1].IsCoinStake;
        }

        public static bool IsProofOfWork(Block block)
        {
            return !IsProofOfStake(block);
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.flags);
            stream.ReadWrite(ref this.Mint);
            stream.ReadWrite(ref this.StakeModifier);
            stream.ReadWrite(ref this.StakeModifierV2);
            if (this.IsProofOfStake())
            {
                stream.ReadWrite(ref this.PrevoutStake);
                stream.ReadWrite(ref this.StakeTime);
            }
            stream.ReadWrite(ref this.HashProof);
        }

        public bool IsProofOfWork()
        {
            return !((this.Flags & BlockFlag.BLOCK_PROOF_OF_STAKE) > 0);
        }

        public bool IsProofOfStake()
        {
            return (this.Flags & BlockFlag.BLOCK_PROOF_OF_STAKE) > 0;
        }

        public void SetProofOfStake()
        {
            this.Flags |= BlockFlag.BLOCK_PROOF_OF_STAKE;
        }

        public uint GetStakeEntropyBit()
        {
            return (uint)(this.Flags & BlockFlag.BLOCK_STAKE_ENTROPY) >> 1;
        }

        public bool SetStakeEntropyBit(uint nEntropyBit)
        {
            if (nEntropyBit > 1)
                return false;
            this.Flags |= (nEntropyBit != 0 ? BlockFlag.BLOCK_STAKE_ENTROPY : 0);
            return true;
        }

        /// <summary>
        /// Constructs a stake block from a given block.
        /// </summary>
        public static BlockStake Load(Block block)
        {
            var blockStake = new BlockStake
            {
                StakeModifierV2 = uint256.Zero,
                HashProof = uint256.Zero
            };

            if (IsProofOfStake(block))
            {
                blockStake.SetProofOfStake();
                blockStake.StakeTime = block.Transactions[1].Time;
                blockStake.PrevoutStake = block.Transactions[1].Inputs[0].PrevOut;
            }

            return blockStake;
        }

        /// <summary>
        /// Constructs a stake block from a set bytes and the given network.
        /// </summary>
        public static BlockStake Load(byte[] bytes, Network network)
        {
            var blockStake = new BlockStake();
            blockStake.ReadWrite(bytes, network.Consensus.ConsensusFactory);
            return blockStake;
        }

        /// <summary>
        /// Check PoW and that the blocks connect correctly
        /// </summary>
        /// <param name="network">The network being used</param>
        /// <param name="chainedHeader">Chained block header</param>
        /// <returns>True if PoW is correct</returns>
        public static bool Validate(Network network, ChainedHeader chainedHeader)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            if (chainedHeader.Height != 0 && chainedHeader.Previous == null)
                return false;

            bool heightCorrect = chainedHeader.Height == 0 || chainedHeader.Height == chainedHeader.Previous.Height + 1;
            bool genesisCorrect = chainedHeader.Height != 0 || chainedHeader.HashBlock == network.GetGenesis().GetHash();
            bool hashPrevCorrect = chainedHeader.Height == 0 || chainedHeader.Header.HashPrevBlock == chainedHeader.Previous.HashBlock;
            bool hashCorrect = chainedHeader.HashBlock == chainedHeader.Header.GetHash();

            return heightCorrect && genesisCorrect && hashPrevCorrect && hashCorrect;
        }
    }

    /// <summary>
    /// A Proof Of Stake transaction.
    /// </summary>
    /// <remarks>
    /// TODO: later we can move the POS timestamp field in this class.
    /// serialization can be refactored to have a common array that will be serialized and each inheritance can add to the array)
    /// </remarks>
    public class PosTransaction : Transaction
    {
        public bool IsColdCoinStake { get; set; }

        public PosTransaction() : base()
        {
        }

        public PosTransaction(string hex, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION) : this()
        {
            this.FromBytes(Encoders.Hex.DecodeData(hex), version);
        }

        public PosTransaction(byte[] bytes) : this()
        {
            this.FromBytes(bytes);
        }

        public override bool IsProtocolTransaction()
        {
            return this.IsCoinStake || this.IsCoinBase;
        }
    }

    public class ProvenHeaderConsensusFactory : PosConsensusFactory
    {
        public override BlockHeader CreateBlockHeader()
        {
            return base.CreateProvenBlockHeader();
        }
    }

    /// <summary>
    /// The consensus factory for creating POS protocol types.
    /// </summary>
    public class PosConsensusFactory : ConsensusFactory
    {
        public PosConsensusFactory()
            : base()
        {
        }

        /// <inheritdoc />
        public override Block CreateBlock()
        {
            return new PosBlock(this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new PosBlockHeader();
        }

        public ProvenBlockHeader CreateProvenBlockHeader()
        {
            return new ProvenBlockHeader();
        }

        public ProvenBlockHeader CreateProvenBlockHeader(PosBlock block)
        {
            var provenBlockHeader = new ProvenBlockHeader(block);

            // Serialize the size.
            provenBlockHeader.ToBytes(this);

            return provenBlockHeader;
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new PosTransaction();
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(string hex)
        {
            return new PosTransaction(hex);
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(byte[] bytes)
        {
            return new PosTransaction(bytes);
        }
    }

    /// <summary>
    /// A POS block header, this will create a work hash based on the X13 hash algos.
    /// </summary>
#pragma warning disable 618
    public class PosBlockHeader : BlockHeader
#pragma warning restore 618
    {
        /// <inheritdoc />
        public override int CurrentVersion => 7;

        /// <inheritdoc />
        public override uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] innerHashes = this.hashes;

            if (innerHashes != null)
                hash = innerHashes[0];

            if (hash != null)
                return hash;

            if (this.version > 6)
            {
                using (var hs = new HashStream())
                {
                    this.ReadWriteHashingStream(new BitcoinStream(hs, true));
                    hash = hs.GetHash();
                }
            }
            else
            {
                hash = this.GetPoWHash();
            }

            innerHashes = this.hashes;
            if (innerHashes != null)
            {
                innerHashes[0] = hash;
            }

            return hash;
        }

        /// <inheritdoc />
        public override uint256 GetPoWHash()
        {
            using (var ms = new MemoryStream())
            {
                this.ReadWriteHashingStream(new BitcoinStream(ms, true));
                return HashX13.Instance.Hash(ms.ToArray());
            }
        }
    }

    /// <summary>
    /// A POS block that contains the additional block signature serialization.
    /// </summary>
    public class PosBlock : Block
    {
        /// <summary>
        /// A block signature - signed by one of the coin base txout[N]'s owner.
        /// </summary>
        private BlockSignature blockSignature = new BlockSignature();

        public PosBlock(BlockHeader blockHeader) : base(blockHeader)
        {
        }

        /// <summary>
        /// The block signature type.
        /// </summary>
        public BlockSignature BlockSignature
        {
            get { return this.blockSignature; }
            set { this.blockSignature = value; }
        }

        /// <summary>
        /// The additional serialization of the block POS block.
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);
            stream.ReadWrite(ref this.blockSignature);

            this.BlockSize = stream.Serializing ? stream.Counter.WrittenBytes : stream.Counter.ReadBytes;
        }

        /// <summary>
        /// Gets the block's coinstake transaction or returns the coinbase transaction if there is no coinstake.
        /// </summary>
        /// <returns>Coinstake transaction or coinbase transaction.</returns>
        /// <remarks>
        /// <para>In PoS blocks, coinstake transaction is the second transaction in the block.</para>
        /// <para>In PoW there isn't a coinstake transaction, return coinbase instead to be able to compute stake modifier for the next eventual PoS block.</para>
        /// </remarks>
        public Transaction GetProtocolTransaction()
        {
            return (this.Transactions.Count > 1 && this.Transactions[1].IsCoinStake) ? this.Transactions[1] : this.Transactions[0];
        }
    }
}