using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    /// <summary>
    /// A BlockHeader chained with all its ancestors.
    /// </summary>
    public class ChainedBlock
    {
        /// <summary>Value of 2^256.</summary>
        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        /// <summary>Window length for calculating median time span.</summary>
        private const int MedianTimeSpan = 11;

        /// <summary>The hash of the block which is also known as the block id.</summary>
        public uint256 HashBlock { get; private set; }

        /// <summary>Predecessor of this block.</summary>
        public ChainedBlock Previous { get; private set; }

        /// <summary>Block to navigate from this block to the next in the skip list.</summary>
        public ChainedBlock Skip { get; private set; }

        /// <summary>Height of the entry in the chain. The genesis block has height 0.</summary>
        public int Height { get; private set; }

        /// <summary>Block header for this entry.</summary>
        public BlockHeader Header { get; private set; }

        /// <summary>Integer representation of the <see cref="ChainWork"/>.</summary>
        private BigInteger chainWork;

        /// <summary>Total amount of work in the chain up to and including this block.</summary>
        public uint256 ChainWork { get { return Target.ToUInt256(this.chainWork); } }

        /// <summary>
        /// Constructs a chained block.
        /// </summary>
        /// <param name="header">Header for the block.</param>
        /// <param name="headerHash">Hash of the header of the block.</param>
        /// <param name="previous">Link to the previous block in the chain.</param>
        public ChainedBlock(BlockHeader header, uint256 headerHash, ChainedBlock previous)
        {
            this.Header = header ?? throw new ArgumentNullException("header");
            this.HashBlock = headerHash ?? throw new ArgumentNullException("headerHash");

            if (previous != null)
                this.Height = previous.Height + 1;

            this.Previous = previous;

            if (previous == null)
            {
                if (header.HashPrevBlock != uint256.Zero)
                    throw new ArgumentException("Only the genesis block can have no previous block");
            }
            else
            {
                if (previous.HashBlock != header.HashPrevBlock)
                    throw new ArgumentException("The previous block has not the expected hash");

                // Calculates the location of the skip block for this block.
                this.Skip = this.Previous.GetAncestor(this.GetSkipHeight(this.Height));
            }

            this.CalculateChainWork();
        }

        /// <summary>
        /// Constructs a chained block at the start of a chain.
        /// </summary>
        /// <param name="header">The header for the block.</param>
        /// <param name="headerHash">The hash computed according to NetworkOptions.</param>
        /// <param name="height">The height of the block.</param>
        public ChainedBlock(BlockHeader header, uint256 headerHash, int height)
        {
            this.Header = header ?? throw new ArgumentNullException("header");
            this.Height = height;
            this.HashBlock = headerHash;
            this.CalculateChainWork();
        }

        /// <summary>
        /// Calculates the total amount of work in the chain up to and including this block.
        /// </summary>
        private void CalculateChainWork()
        {
            this.chainWork = (this.Previous == null ? BigInteger.Zero : this.Previous.chainWork).Add(this.GetBlockProof());
        }

        /// <summary>Calculates the amount of work that this block contributes to the total chain work.</summary>
        /// <returns>Amount of work.</returns>
        private BigInteger GetBlockProof()
        {
            BigInteger target = this.Header.Bits.ToBigInteger();
            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(Pow256) >= 0))
                return BigInteger.Zero;

            return Pow256.Divide(target.Add(BigInteger.One));
        }

        /// <summary>Gets a <see cref="BlockLocator"/> for this chain entry.</summary>
        /// <returns>A block locator for this chain entry.</returns>
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
                pindex = this.GetAncestor(height);

                if (blockHashes.Count > 10)
                    nStep *= 2;
            }

            var locators = new BlockLocator();
            locators.Blocks = blockHashes;
            return locators;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            ChainedBlock item = obj as ChainedBlock;
            if (item == null)
                return false;

            return this.HashBlock.Equals(item.HashBlock);
        }

        /// <inheritdoc />
        public static bool operator ==(ChainedBlock a, ChainedBlock b)
        {
            if (System.Object.ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.HashBlock == b.HashBlock;
        }

        /// <inheritdoc />
        public static bool operator !=(ChainedBlock a, ChainedBlock b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.HashBlock.GetHashCode();
        }

        /// <summary>
        /// Enumerator from this entry in the chain to the genesis block.
        /// </summary>
        /// <returns>The enumeration of the chain.</returns>
        public IEnumerable<ChainedBlock> EnumerateToGenesis()
        {
            ChainedBlock current = this;
            while (current != null)
            {
                yield return current;
                current = current.Previous;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Height + "-" + this.HashBlock;
        }

        /// <summary>
        /// Finds the ancestor of this entry in the chain that matches the chained block header specified.
        /// </summary>
        /// <param name="chainedBlockHeader">The chained block header to search for.</param>
        /// <returns>The chained block header or <c>null</c> if can't be found.</returns>
        /// <remarks>This method compares the hash of the block header at the same height in the current chain 
        /// to verify the correct chained block header has been found.</remarks>
        public ChainedBlock FindAncestorOrSelf(ChainedBlock chainedBlockHeader)
        {
            ChainedBlock found = this.GetAncestor(chainedBlockHeader.Height);
            if ((found != null) && (found.HashBlock == chainedBlockHeader.HashBlock))
                return found;

            return null;
        }

        /// <summary>
        /// Finds the ancestor of this entry in the chain that matches the block hash given.
        /// </summary>
        /// <param name="blockHash">The block hash to search for.</param>
        /// <returns>The ancestor of this chain that matches the block hash.</returns>
        public ChainedBlock FindAncestorOrSelf(uint256 blockHash)
        {
            ChainedBlock currentBlock = this;
            while ((currentBlock != null) && (currentBlock.HashBlock != blockHash))
            {
                currentBlock = currentBlock.Previous;
            }

            return currentBlock;
        }

        /// <summary>
        /// Gets the proof of work target for a potential new block after this entry on the chain.
        /// </summary>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(Network network)
        {
            return this.GetNextWorkRequired(network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for a potential new block after this entry on the chain.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(Consensus consensus)
        {
            BlockHeader dummy = consensus.ConsensusFactory.CreateBlockHeader();
            dummy.HashPrevBlock = this.HashBlock;
            dummy.BlockTime = DateTimeOffset.UtcNow;
            return this.GetNextWorkRequired(dummy, consensus);
        }

        /// <summary>
        /// Gets the proof of work target for the new block specified.
        /// </summary>
        /// <param name="block">The new block to get proof of work for.</param>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(BlockHeader block, Network network)
        {
            return this.GetNextWorkRequired(block, network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for the new block specified.
        /// </summary>
        /// <param name="block">The new block to get proof of work for.</param>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(BlockHeader block, Consensus consensus)
        {
            return new ChainedBlock(block, block.GetHash(), this).GetWorkRequired(consensus);
        }

        /// <summary>
        /// Gets the proof of work target for this entry in the chain.
        /// </summary>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetWorkRequired(Network network)
        {
            return this.GetWorkRequired(network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for this entry in the chain.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
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

            // Go back by what we want to be 14 days worth of blocks.
            long pastHeight = lastBlock.Height - (consensus.DifficultyAdjustmentInterval - 1);

            ChainedBlock firstChainedBlock = this.GetAncestor((int)pastHeight);
            if (firstChainedBlock == null)
                throw new NotSupportedException("Can only calculate work of a full chain");

            if (consensus.PowNoRetargeting)
                return lastBlock.Header.Bits;

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

        /// <summary>
        /// Calculate the median block time over <see cref="MedianTimeSpan"/> window from this entry in the chain.
        /// </summary>
        /// <returns>The median block time.</returns>
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

        /// <summary>
        /// Check that the header is a valid block header including the work done for PoW blocks.
        /// </summary>
        /// <param name="network">The network to verify against.</param>
        /// <returns><c>true</c> if the header is a valid block header, <c>false</c> otherwise.</returns>
        public bool Validate(Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            if (network.Consensus.IsProofOfStake)
                return BlockStake.Validate(network, this);

            bool genesisCorrect = (this.Height != 0) || this.HashBlock == network.GetGenesis().GetHash();
            return genesisCorrect && this.Validate(network.Consensus);
        }

        /// <summary>
        /// Check PoW against consensus and that the blocks connect correctly.
        /// </summary>
        /// <param name="consensus">The consensus rules being used.</param>
        /// <returns><c>true</c> if the header is a valid block header, <c>false</c> otherwise.</returns>
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

        /// <summary>
        /// Verify proof of work of the header of this chain using consensus.
        /// </summary>
        /// <param name="network">The network to verify proof of work on.</param>
        /// <returns>Whether proof of work is valid.</returns>
        public bool CheckProofOfWorkAndTarget(Network network)
        {
            return this.CheckProofOfWorkAndTarget(network.Consensus);
        }

        /// <summary>
        /// Verify proof of work of the header of this chain using consensus.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this validation.</param>
        /// <returns>Whether proof of work is valid.</returns>
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

            highChain = highChain.GetAncestor(lowChain.Height);

            while (highChain.HashBlock != lowChain.HashBlock)
            {
                if (highChain.Skip != lowChain.Skip)
                {
                    highChain = highChain.Skip;
                    lowChain = lowChain.Skip;
                }
                else
                {
                    lowChain = lowChain.Previous;
                    highChain = highChain.Previous;
                }

                if ((lowChain == null) || (highChain == null))
                    return null;
            }

            return highChain;
        }

        /// <summary>
        /// Finds the ancestor of this entry in the chain that matches the block height given.
        /// <remarks>Note: This uses a skiplist to improve list navigation performance.</remarks>
        /// </summary>
        /// <param name="ancestorHeight">The block height to search for.</param>
        /// <returns>The ancestor of this chain that matches the block height.</returns>
        public ChainedBlock GetAncestor(int ancestorHeight)
        {
            if (ancestorHeight > this.Height)
                return null;

            ChainedBlock walk = this;
            while ((walk != null) && (walk.Height != ancestorHeight))
            {
                // No skip so follow previous.
                if (walk.Skip == null)
                {
                    walk = walk.Previous;
                    continue;
                }

                // Skip is at target.
                if (walk.Skip.Height == ancestorHeight)
                    return walk.Skip;

                // Only follow skip if Previous.skip isn't better than skip.Previous.
                int heightSkip = walk.Skip.Height;
                int heightSkipPrev = this.GetSkipHeight(walk.Height - 1);
                bool skipAboveTarget = heightSkip > ancestorHeight;
                bool skipPreviousBetterThanPreviousSkip = !((heightSkipPrev < (heightSkip - 2)) && (heightSkipPrev >= ancestorHeight));
                if (skipAboveTarget && skipPreviousBetterThanPreviousSkip)
                {
                    walk = walk.Skip;
                    continue;
                }

                walk = walk.Previous;
            }

            return walk;
        }

        /// <summary>
        /// Compute what height to jump back to for the skip block given this height.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/master/src/chain.cpp#L72-L81"/>
        /// </summary>
        /// <param name="height">Height to compute skip height for.</param>
        /// <returns>The height to skip to.</returns>
        private int GetSkipHeight(int height)
        {
            if (height < 2)
                return 0;

            // Determine which height to jump back to. Any number strictly lower than height is acceptable,
            // but the following expression was taken from bitcoin core. There it was tested in simulations
            // and performed well.
            // Skip steps are exponential - Using skip, max 110 steps to go back up to 2^18 blocks.
            return (height & 1) != 0 ? this.InvertLowestOne(this.InvertLowestOne(height - 1)) + 1 : this.InvertLowestOne(height);
        }

        /// <summary>
        /// Turn the lowest '1' bit in the binary representation of a number into a '0'.
        /// </summary>
        /// <param name="n">Number to invert lowest bit.</param>
        /// <returns>New number.</returns>
        private int InvertLowestOne(int n)
        {
            return n & (n - 1);
        }
    }
}