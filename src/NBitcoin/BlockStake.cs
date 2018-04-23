using System;
using System.Linq;

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
            var hashCorrect = chainedBlock.HashBlock == chainedBlock.Header.GetHash(network.NetworkOptions);

            return heightCorrect && genesisCorrect && hashPrevCorrect && hashCorrect;
        }
    }

    public partial class Block
    {
        public static bool BlockSignature = false;

        // block signature - signed by one of the coin base txout[N]'s owner
        private BlockSignature blockSignature = new BlockSignature();

        public BlockSignature BlockSignatur
        {
            get { return this.blockSignature; }
            set { this.blockSignature = value; }
        }
    }
}