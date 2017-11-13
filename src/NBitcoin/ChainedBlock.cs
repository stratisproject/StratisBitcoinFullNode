using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    /// <summary>
    /// A BlockHeader chained with all its ancestors.
    /// </summary>
    public class ChainedBlock
    {
        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        private const int MedianTimeSpan = 11;

        // Pointer to the hash of the block, if any. memory is owned by this CBlockIndex.
        private uint256 hashBlock;
        public uint256 HashBlock { get { return this.hashBlock; } }

        // Pointer to the index of the predecessor of this block.
        private ChainedBlock previous;
        public ChainedBlock Previous { get { return this.previous; } }

        // Height of the entry in the chain. The genesis block has height 0.
        private int height;
        public int Height { get { return this.height; } }

        private BlockHeader header;
        public BlockHeader Header { get { return this.header; } }

        private BigInteger chainWork;
        public uint256 ChainWork
        {
            get
            {
                return Target.ToUInt256(this.chainWork);
            }
        }

        public ChainedBlock(BlockHeader header, uint256 headerHash, ChainedBlock previous)
        {
            this.header = header ?? throw new ArgumentNullException("header");

            if (previous != null)
                this.height = previous.Height + 1;

            this.previous = previous;
            this.hashBlock = headerHash ?? header.GetHash();

            if (previous == null)
            {
                if (header.HashPrevBlock != uint256.Zero)
                    throw new ArgumentException("Only the genesis block can have no previous block");
            }
            else
            {
                if (previous.HashBlock != header.HashPrevBlock)
                    throw new ArgumentException("The previous block has not the expected hash");
            }

            this.CalculateChainWork();
        }

        private void CalculateChainWork()
        {
            this.chainWork = (this.Previous == null ? BigInteger.Zero : this.Previous.chainWork).Add(GetBlockProof());
        }

        private BigInteger GetBlockProof()
        {
            BigInteger target = this.Header.Bits.ToBigInteger();
            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(Pow256) >= 0))
                return BigInteger.Zero;

            // We need to compute 2**256 / (bnTarget+1), but we can't represent 2**256
            // as it's too large for a arith_uint256. However, as 2**256 is at least as large
            // as bnTarget+1, it is equal to ((2**256 - bnTarget - 1) / (bnTarget+1)) + 1,
            // or ~bnTarget / (nTarget+1) + 1.
            return ((Pow256.Subtract(target).Subtract(BigInteger.One)).Divide(target.Add(BigInteger.One))).Add(BigInteger.One);
        }

        public ChainedBlock(BlockHeader header, int height)
        {
            this.header = header ?? throw new ArgumentNullException("header");
            this.height = height;
            this.hashBlock = header.GetHash();
            this.CalculateChainWork();
        }

        public BlockLocator GetLocator()
        {
            int nStep = 1;
            List<uint256> blockHashes = new List<uint256>();

            ChainedBlock pindex = this;
            while (pindex != null)
            {
                blockHashes.Add(pindex.HashBlock);
                // Stop when we have added the genesis block.
                if (pindex.Height == 0)
                    break;

                // Exponentially larger steps back, plus the genesis block.
                int height = Math.Max(pindex.Height - nStep, 0);

                while (pindex.Height > height)
                    pindex = pindex.Previous;

                if (blockHashes.Count > 10)
                    nStep *= 2;
            }

            var locators = new BlockLocator();
            locators.Blocks = blockHashes;
            return locators;
        }

        public override bool Equals(object obj)
        {
            ChainedBlock item = obj as ChainedBlock;
            if (item == null)
                return false;

            return this.hashBlock.Equals(item.hashBlock);
        }
        public static bool operator ==(ChainedBlock a, ChainedBlock b)
        {
            if (System.Object.ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.hashBlock == b.hashBlock;
        }

        public static bool operator !=(ChainedBlock a, ChainedBlock b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.hashBlock.GetHashCode();
        }

        public IEnumerable<ChainedBlock> EnumerateToGenesis()
        {
            ChainedBlock current = this;
            while (current != null)
            {
                yield return current;
                current = current.Previous;
            }
        }

        public override string ToString()
        {
            return this.Height + "-" + this.HashBlock;
        }

        public ChainedBlock FindAncestorOrSelf(int height)
        {
            if (height > this.Height)
                throw new InvalidOperationException("Can only find blocks below or equals to current height");

            if (height < 0)
                throw new ArgumentOutOfRangeException("height");

            ChainedBlock currentBlock = this;
            while (height != currentBlock.Height)
            {
                currentBlock = currentBlock.Previous;
            }

            return currentBlock;
        }

        public ChainedBlock FindAncestorOrSelf(uint256 blockHash)
        {
            ChainedBlock currentBlock = this;
            while ((currentBlock != null) && (currentBlock.HashBlock != blockHash))
            {
                currentBlock = currentBlock.Previous;
            }

            return currentBlock;
        }

        public Target GetWorkRequired(Network network)
        {
            return this.GetWorkRequired(network.Consensus);
        }

        public Target GetNextWorkRequired(Network network)
        {
            return this.GetNextWorkRequired(network.Consensus);
        }

        public Target GetNextWorkRequired(Consensus consensus)
        {
            BlockHeader dummy = new BlockHeader();
            dummy.HashPrevBlock = this.HashBlock;
            dummy.BlockTime = DateTimeOffset.UtcNow;
            return GetNextWorkRequired(dummy, consensus);
        }

        public Target GetNextWorkRequired(BlockHeader block, Network network)
        {
            return this.GetNextWorkRequired(block, network.Consensus);
        }

        public Target GetNextWorkRequired(BlockHeader block, Consensus consensus)
        {
            return new ChainedBlock(block, block.GetHash(), this).GetWorkRequired(consensus);
        }

        public Target GetWorkRequired(Consensus consensus)
        {
            // Genesis block.
            if (this.Height == 0)
                return consensus.PowLimit;

            Target proofOfWorkLimit = consensus.PowLimit;
            ChainedBlock lastBlock = this.Previous;
            var height = this.Height;

            if (lastBlock == null)
                return proofOfWorkLimit;

            // Only change once per interval.
            if ((height) % consensus.DifficultyAdjustmentInterval != 0)
            {
                if (consensus.PowAllowMinDifficultyBlocks)
                {
                    // Special difficulty rule for testnet:
                    // If the new block's timestamp is more than 2* 10 minutes
                    // then allow mining of a min-difficulty block.
                    if (this.Header.BlockTime > (lastBlock.Header.BlockTime + TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * 2)))
                        return proofOfWorkLimit;
                 
                    // Return the last non-special-min-difficulty-rules-block.
                    ChainedBlock chainedBlock = lastBlock;
                    while ((chainedBlock.Previous != null) && ((chainedBlock.Height % consensus.DifficultyAdjustmentInterval) != 0) && (chainedBlock.Header.Bits == proofOfWorkLimit))
                        chainedBlock = chainedBlock.Previous;

                    return chainedBlock.Header.Bits;
                }

                return lastBlock.Header.Bits;
            }

            long pastHeight = 0;
            if (consensus.LitecoinWorkCalculation)
            {
                long blocksToGoBack = consensus.DifficultyAdjustmentInterval - 1;
                if ((lastBlock.Height + 1) != consensus.DifficultyAdjustmentInterval)
                    blocksToGoBack = consensus.DifficultyAdjustmentInterval;

                pastHeight = lastBlock.Height - blocksToGoBack;
            }
            else
            {
                // Go back by what we want to be 14 days worth of blocks.
                pastHeight = lastBlock.Height - (consensus.DifficultyAdjustmentInterval - 1);
            }

            ChainedBlock firstChainedBlock = this.EnumerateToGenesis().FirstOrDefault(o => o.Height == pastHeight);
            assert(firstChainedBlock);

            if (consensus.PowNoRetargeting)
                return lastBlock.header.Bits;

            // Limit adjustment step.
            TimeSpan actualTimespan = lastBlock.Header.BlockTime - firstChainedBlock.Header.BlockTime;
            if (actualTimespan < TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4))
                actualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4);
            if (actualTimespan > TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4))
                actualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4);

            // Retarget.
            BigInteger newTarget = lastBlock.Header.Bits.ToBigInteger();
            newTarget = newTarget.Multiply(BigInteger.ValueOf((long)actualTimespan.TotalSeconds));
            newTarget = newTarget.Divide(BigInteger.ValueOf((long)consensus.PowTargetTimespan.TotalSeconds));

            var finalTarget = new Target(newTarget);
            if (finalTarget > proofOfWorkLimit)
                finalTarget = proofOfWorkLimit;

            return finalTarget;
        }

        public DateTimeOffset GetMedianTimePast()
        {
            DateTimeOffset[] median = new DateTimeOffset[MedianTimeSpan];
            int begin = MedianTimeSpan;
            int end = MedianTimeSpan;

            ChainedBlock chainedBlock = this;
            for (int i = 0; i < MedianTimeSpan && chainedBlock != null; i++, chainedBlock = chainedBlock.Previous)
                median[--begin] = chainedBlock.Header.BlockTime;

            Array.Sort(median);
            return median[begin + ((end - begin) / 2)];
        }

        private static void assert(object obj)
        {
            if (obj == null)
                throw new NotSupportedException("Can only calculate work of a full chain");
        }

        /// <summary>
        /// Check PoW and that the blocks connect correctly.
        /// </summary>
        /// <param name="network">The network being used.</param>
        /// <returns>True if PoW is correct.</returns>
        public bool Validate(Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            if (Block.BlockSignature)
                return BlockStake.Validate(network, this);

            bool genesisCorrect = (this.Height != 0) || this.HashBlock == network.GetGenesis().GetHash();
            return genesisCorrect && this.Validate(network.Consensus);
        }

        /// <summary>
        /// Check PoW and that the blocks connect correctly.
        /// </summary>
        /// <param name="consensus">The consensus being used.</param>
        /// <returns>True if PoW is correct.</returns>
        public bool Validate(Consensus consensus)
        {
            if (consensus == null)
                throw new ArgumentNullException("consensus");

            if ((this.Height != 0) && (this.Previous == null))
                return false;

            bool heightCorrect = (this.Height == 0) || (this.Height == this.Previous.Height + 1);
            bool hashPrevCorrect = (this.Height == 0) || (this.Header.HashPrevBlock == this.Previous.HashBlock);
            bool hashCorrect = this.HashBlock == this.Header.GetHash();
            bool workCorrect = this.CheckProofOfWorkAndTarget(consensus);

            return heightCorrect && hashPrevCorrect && hashCorrect && workCorrect;
        }

        public bool CheckProofOfWorkAndTarget(Network network)
        {
            return this.CheckProofOfWorkAndTarget(network.Consensus);
        }

        public bool CheckProofOfWorkAndTarget(Consensus consensus)
        {
            return (this.Height == 0) || (this.Header.CheckProofOfWork(consensus) && this.Header.Bits == GetWorkRequired(consensus));
        }


        /// <summary>
        /// Find first common block between two chains.
        /// </summary>
        /// <param name="block">The tip of the other chain.</param>
        /// <returns>First common block or <c>null</c>.</returns>
        public ChainedBlock FindFork(ChainedBlock block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            ChainedBlock highChain = this.Height > block.Height ? this : block;
            ChainedBlock lowChain = highChain == this ? block : this;
            while (highChain.Height != lowChain.Height)
            {
                highChain = highChain.Previous;
            }

            while (highChain.HashBlock != lowChain.HashBlock)
            {
                lowChain = lowChain.Previous;
                highChain = highChain.Previous;
                if ((lowChain == null) || (highChain == null))
                    return null;
            }

            return highChain;
        }

        public ChainedBlock GetAncestor(int height)
        {
            if ((height > this.Height) || (height < 0))
                return null;

            ChainedBlock current = this;

            while (true)
            {
                if (current.Height == height)
                    return current;

                current = current.Previous;
            }
        }
    }
}