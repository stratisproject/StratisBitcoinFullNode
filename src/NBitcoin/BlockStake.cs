using System;
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

        public BlockStake(byte[] bytes)
        {
            this.ReadWrite(bytes);
        }

        public BlockStake(Block block)
        {
            this.StakeModifierV2 = uint256.Zero;
            this.HashProof = uint256.Zero;

            if (IsProofOfStake(block))
            {
                this.SetProofOfStake();
                this.StakeTime = block.Transactions[1].Time;
                this.PrevoutStake = block.Transactions[1].Inputs[0].PrevOut;
            }
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
        /// Check PoW and that the blocks connect correctly
        /// </summary>
        /// <param name="network">The network being used</param>
        /// <param name="chainedBlock">The chain representing a block header.</param>
        /// <returns>True if PoW is correct</returns>
        public static bool Validate(Network network, ChainedBlock chainedBlock)
        {
            if (network == null)
                throw new ArgumentNullException("network");
            if (chainedBlock.Height != 0 && chainedBlock.Previous == null)
                return false;
            var heightCorrect = chainedBlock.Height == 0 || chainedBlock.Height == chainedBlock.Previous.Height + 1;
            var genesisCorrect = chainedBlock.Height != 0 || chainedBlock.HashBlock == network.GetGenesis().GetHash();
            var hashPrevCorrect = chainedBlock.Height == 0 || chainedBlock.Header.HashPrevBlock == chainedBlock.Previous.HashBlock;
            var hashCorrect = chainedBlock.HashBlock == chainedBlock.Header.GetHash();

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
    }

    /// <summary>
    /// The consensus factory for creating POS protocol types.
    /// </summary>
    public class PosConsensusFactory : ConsensusFactory
    {
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

        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new PosTransaction();
        }
    }

    /// <summary>
    /// A POS block header, this will create a work hash based on the X13 hash algos.
    /// </summary>
    public class PosBlockHeader : BlockHeader
    {
        /// <summary>Current header version.</summary>
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
                hash = Hashes.Hash256(this.ToBytes());
            else
                hash = this.GetPoWHash();

            innerHashes = this.hashes;
            if (innerHashes != null)
            {
                innerHashes[0] = hash;
            }

            return hash;
        }

        /// <summary>
        /// Generate a has based on the X13 algorithms.
        /// </summary>
        /// <returns></returns>
        public override uint256 GetPoWHash()
        {
            return HashX13.Instance.Hash(this.ToBytes());
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

        internal PosBlock(BlockHeader blockHeader) : base(blockHeader)
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
        }
    }
}