using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    /// <summary>
    /// Represents the availability state of a block.
    /// </summary>
    public enum BlockDataAvailabilityState
    {
        /// <summary>
        /// A <see cref="BlockHeader"/> is present, the block data is not available.
        /// </summary>
        HeaderOnly,

        /// <summary>
        /// We are interested in downloading the <see cref="Block"/> that is being represented by the current <see cref="BlockHeader"/>.
        /// This happens when we don't have block which is represented by this header and the header is a part of a chain that
        /// can potentially replace our consensus tip because its chain work is greater than our consensus tip's chain work.
        /// </summary>
        BlockRequired,

        /// <summary>
        /// The <see cref="Block"/> was downloaded and is available, but it may not be reachable directly but via a store.
        /// </summary>
        BlockAvailable
    }

    /// <summary>
    /// Represents the validation state of a block.
    /// </summary>
    public enum ValidationState
    {
        /// <summary>
        /// We have a valid block header.
        /// </summary>
        HeaderValidated,

        /// <summary>
        /// Validated using all rules that don't require change of state.
        /// Some validation rules may be skipped for blocks previously marked as assumed valid.
        /// </summary>
        PartiallyValidated,

        /// <summary>
        /// Validated using all the rules.
        /// Some validation rules may be skipped for blocks previously marked as assumed valid.
        /// </summary>
        FullyValidated
    }

    /// <summary>
    /// A BlockHeader chained with all its ancestors.
    /// </summary>
    public class ChainedHeader
    {
        /// <summary>Value of 2^256.</summary>
        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        /// <summary>Window length for calculating median time span.</summary>
        private const int MedianTimeSpan = 11;

        /// <summary>The hash of the block which is also known as the block id.</summary>
        public uint256 HashBlock { get; private set; }

        /// <summary>Predecessor of this block.</summary>
        public ChainedHeader Previous { get; private set; }

        /// <summary>Block to navigate from this block to the next in the skip list.</summary>
        public ChainedHeader Skip { get; private set; }

        /// <summary>Height of the entry in the chain. The genesis block has height 0.</summary>
        public int Height { get; private set; }

        /// <summary>Block header for this entry.</summary>
        public BlockHeader Header { get; private set; }

        /// <summary>Integer representation of the <see cref="ChainWork"/>.</summary>
        private BigInteger chainWork;

        /// <summary>Total amount of work in the chain up to and including this block.</summary>
        public uint256 ChainWork { get { return Target.ToUInt256(this.chainWork); } }

        /// <inheritdoc cref="BlockDataAvailabilityState" />
        public BlockDataAvailabilityState BlockDataAvailability { get; set; }

        /// <inheritdoc cref="ValidationState" />
        public ValidationState BlockValidationState { get; set; }

        /// <summary>
        /// An indicator that the current instance of <see cref="ChainedHeader"/> has been disconnected from the previous instance.
        /// </summary>
        public bool IsReferenceConnected
        {
            get { return this.Previous.Next.Any(c => ReferenceEquals(c, this)); }
        }

        /// <summary>
        /// Block represented by this header is assumed to be valid and only subset of validation rules should be applied to it.
        /// This state is used for blocks before the last checkpoint or for blocks that are on the chain of assume valid block.
        /// </summary>
        public bool IsAssumedValid { get; set; }

        /// <summary>A pointer to the block data if available (this can be <c>null</c>), its availability will be represented by <see cref="BlockDataAvailability"/>.</summary>
        public Block Block { get; set; }

        /// <summary>
        /// Points to the next <see cref="ChainedHeader"/>, if a new branch of the chain is presented there can be more then one <see cref="Next"/> header.
        /// </summary>
        public List<ChainedHeader> Next { get; private set; }

        /// <summary>
        /// Constructs a chained block.
        /// </summary>
        /// <param name="header">Header for the block.</param>
        /// <param name="headerHash">Hash of the header of the block.</param>
        /// <param name="previous">Link to the previous block in the chain.</param>
        public ChainedHeader(BlockHeader header, uint256 headerHash, ChainedHeader previous) : this(header, headerHash)
        {
            if (previous != null)
                this.Height = previous.Height + 1;

            if (this.Height == 0)
            {
                this.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
                this.BlockValidationState = ValidationState.FullyValidated;
            }

            this.Previous = previous;

            if (previous == null)
            {
                if (header.HashPrevBlock != uint256.Zero)
                    throw new ArgumentException("Only the genesis block can have no previous block");
            }
            else
            {
                if (previous.HashBlock != header.HashPrevBlock)
                    throw new ArgumentException("The previous block does not have the expected hash");

                // Calculates the location of the skip block for this block.
                this.Skip = this.Previous.GetAncestor(this.GetSkipHeight(this.Height));
            }

            this.CalculateChainWork();
        }

        /// <summary>
        /// Constructs a chained header at the start of a chain.
        /// </summary>
        /// <param name="header">The header for the block.</param>
        /// <param name="headerHash">The hash computed according to NetworkOptions.</param>
        /// <param name="height">The height of the block.</param>
        public ChainedHeader(BlockHeader header, uint256 headerHash, int height) : this(header, headerHash)
        {
            this.Height = height;
            this.CalculateChainWork();

            if (height == 0)
            {
                this.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
                this.BlockValidationState = ValidationState.FullyValidated;
            }
        }

        /// <summary>
        /// Constructs a chained header at the start of a chain.
        /// </summary>
        /// <param name="header">The header for the block.</param>
        /// <param name="headerHash">The hash of the block's header.</param>
        private ChainedHeader(BlockHeader header, uint256 headerHash)
        {
            this.Header = header ?? throw new ArgumentNullException(nameof(header));
            this.HashBlock = headerHash ?? throw new ArgumentNullException(nameof(headerHash));
            this.Next = new List<ChainedHeader>(1);
        }

        /// <summary>
        /// Calculates the total amount of work in the chain up to and including this block.
        /// </summary>
        private void CalculateChainWork()
        {
            this.chainWork = (this.Previous == null ? BigInteger.Zero : this.Previous.chainWork).Add(GetBlockProof());
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
            var blockHashes = new List<uint256>();

            ChainedHeader pindex = this;
            while (pindex != null)
            {
                blockHashes.Add(pindex.HashBlock);
                // Stop when we have added the genesis block.
                if (pindex.Height == 0)
                    break;

                // Exponentially larger steps back, plus the genesis block.
                int height = Math.Max(pindex.Height - nStep, 0);
                pindex = GetAncestor(height);

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
            var item = obj as ChainedHeader;
            if (item == null)
                return false;

            return this.HashBlock.Equals(item.HashBlock);
        }

        /// <inheritdoc />
        public static bool operator ==(ChainedHeader a, ChainedHeader b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.HashBlock == b.HashBlock;
        }

        /// <inheritdoc />
        public static bool operator !=(ChainedHeader a, ChainedHeader b)
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
        public IEnumerable<ChainedHeader> EnumerateToGenesis()
        {
            ChainedHeader current = this;
            while (current != null)
            {
                yield return current;
                current = current.Previous;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Height + "-" + this.HashBlock + "-" + this.BlockValidationState + (this.Header is ProvenBlockHeader ? " - PH"  : string.Empty);
        }

        /// <summary>
        /// Finds the ancestor of this entry in the chain that matches the chained header specified.
        /// </summary>
        /// <param name="chainedHeader">The chained header to search for.</param>
        /// <returns>The chained block header or <c>null</c> if can't be found.</returns>
        /// <remarks>This method compares the hash of the block header at the same height in the current chain
        /// to verify the correct chained block header has been found.</remarks>
        public ChainedHeader FindAncestorOrSelf(ChainedHeader chainedHeader)
        {
            ChainedHeader found = GetAncestor(chainedHeader.Height);
            if ((found != null) && (found.HashBlock == chainedHeader.HashBlock))
                return found;

            return null;
        }

        /// <summary>
        /// Finds the ancestor of this entry in the chain that matches the block hash.
        /// It will not search lower than the optional height parameter.
        /// </summary>
        /// <param name="blockHash">The block hash to search for.</param>
        /// <param name="height">Optional height to restrict the search to.</param>
        /// <returns>The ancestor of this chain that matches the block hash, or null if it was not found.</returns>
        public ChainedHeader FindAncestorOrSelf(uint256 blockHash, int height = 0)
        {
            ChainedHeader currentBlock = this;
            while ((currentBlock != null) && (currentBlock.Height > height))
            {
                if (currentBlock.HashBlock == blockHash)
                    break;

                currentBlock = currentBlock.Previous;
            }

            return (currentBlock?.HashBlock == blockHash) ? currentBlock : null;
        }

        /// <summary>
        /// Gets the proof of work target for a potential new block after this entry on the chain.
        /// </summary>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(Network network)
        {
            return GetNextWorkRequired(network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for a potential new block after this entry on the chain.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(IConsensus consensus)
        {
            BlockHeader dummy = consensus.ConsensusFactory.CreateBlockHeader();
            dummy.HashPrevBlock = this.HashBlock;
            dummy.BlockTime = DateTimeOffset.UtcNow;
            return GetNextWorkRequired(dummy, consensus);
        }

        /// <summary>
        /// Gets the proof of work target for the new block specified.
        /// </summary>
        /// <param name="block">The new block to get proof of work for.</param>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(BlockHeader block, Network network)
        {
            return GetNextWorkRequired(block, network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for the new block specified.
        /// </summary>
        /// <param name="block">The new block to get proof of work for.</param>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetNextWorkRequired(BlockHeader block, IConsensus consensus)
        {
            return new ChainedHeader(block, block.GetHash(), this).GetWorkRequired(consensus);
        }

        /// <summary>
        /// Gets the proof of work target for this entry in the chain.
        /// </summary>
        /// <param name="network">The network to get target for.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetWorkRequired(Network network)
        {
            return GetWorkRequired(network.Consensus);
        }

        /// <summary>
        /// Gets the proof of work target for this entry in the chain.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this computation.</param>
        /// <returns>The target proof of work.</returns>
        public Target GetWorkRequired(IConsensus consensus)
        {
            // Genesis block.
            if (this.Height == 0)
                return consensus.PowLimit;

            Target proofOfWorkLimit = consensus.PowLimit;
            ChainedHeader lastBlock = this.Previous;
            int height = this.Height;

            if (lastBlock == null)
                return proofOfWorkLimit;

            long difficultyAdjustmentInterval = this.GetDifficultyAdjustmentInterval(consensus);

            // Only change once per interval.
            if ((height) % difficultyAdjustmentInterval != 0)
            {
                if (consensus.PowAllowMinDifficultyBlocks)
                {
                    // Special difficulty rule for testnet:
                    // If the new block's timestamp is more than 2* 10 minutes
                    // then allow mining of a min-difficulty block.
                    if (this.Header.BlockTime > (lastBlock.Header.BlockTime + TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * 2)))
                        return proofOfWorkLimit;

                    // Return the last non-special-min-difficulty-rules-block.
                    ChainedHeader chainedHeader = lastBlock;
                    while ((chainedHeader.Previous != null) && ((chainedHeader.Height % difficultyAdjustmentInterval) != 0) && (chainedHeader.Header.Bits == proofOfWorkLimit))
                        chainedHeader = chainedHeader.Previous;

                    return chainedHeader.Header.Bits;
                }

                return lastBlock.Header.Bits;
            }

            // Go back by what we want to be 14 days worth of blocks.
            long pastHeight = lastBlock.Height - (difficultyAdjustmentInterval - 1);

            ChainedHeader firstChainedHeader = GetAncestor((int)pastHeight);
            if (firstChainedHeader == null)
                throw new NotSupportedException("Can only calculate work of a full chain");

            if (consensus.PowNoRetargeting)
                return lastBlock.Header.Bits;

            // Limit adjustment step.
            TimeSpan actualTimespan = lastBlock.Header.BlockTime - firstChainedHeader.Header.BlockTime;
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
        /// Calculate the difficulty adjustment interval in blocks based on settings defined in <see cref="IConsensus"/>.
        /// </summary>
        /// <returns>The difficulty adjustment interval in blocks.</returns>
        private long GetDifficultyAdjustmentInterval(IConsensus consensus)
        {
            return (long)consensus.PowTargetTimespan.TotalSeconds / (long)consensus.PowTargetSpacing.TotalSeconds;
        }

        /// <summary>
        /// Calculate the median block time over <see cref="MedianTimeSpan"/> window from this entry in the chain.
        /// </summary>
        /// <returns>The median block time.</returns>
        public DateTimeOffset GetMedianTimePast()
        {
            var median = new DateTimeOffset[MedianTimeSpan];
            int begin = MedianTimeSpan;
            int end = MedianTimeSpan;

            ChainedHeader chainedHeader = this;
            for (int i = 0; i < MedianTimeSpan && chainedHeader != null; i++, chainedHeader = chainedHeader.Previous)
                median[--begin] = chainedHeader.Header.BlockTime;

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
            return genesisCorrect && Validate(network.Consensus);
        }

        /// <summary>
        /// Check PoW against consensus and that the blocks connect correctly.
        /// </summary>
        /// <param name="consensus">The consensus rules being used.</param>
        /// <returns><c>true</c> if the header is a valid block header, <c>false</c> otherwise.</returns>
        public bool Validate(IConsensus consensus)
        {
            if (consensus == null)
                throw new ArgumentNullException("consensus");

            if ((this.Height != 0) && (this.Previous == null))
                return false;

            bool heightCorrect = (this.Height == 0) || (this.Height == this.Previous.Height + 1);
            bool hashPrevCorrect = (this.Height == 0) || (this.Header.HashPrevBlock == this.Previous.HashBlock);
            bool hashCorrect = this.HashBlock == this.Header.GetHash();
            bool workCorrect = CheckProofOfWorkAndTarget(consensus);

            return heightCorrect && hashPrevCorrect && hashCorrect && workCorrect;
        }

        /// <summary>
        /// Verify proof of work of the header of this chain using consensus.
        /// </summary>
        /// <param name="network">The network to verify proof of work on.</param>
        /// <returns>Whether proof of work is valid.</returns>
        public bool CheckProofOfWorkAndTarget(Network network)
        {
            return CheckProofOfWorkAndTarget(network.Consensus);
        }

        /// <summary>
        /// Verify proof of work of the header of this chain using consensus.
        /// </summary>
        /// <param name="consensus">Consensus rules to use for this validation.</param>
        /// <returns>Whether proof of work is valid.</returns>
        public bool CheckProofOfWorkAndTarget(IConsensus consensus)
        {
            return (this.Height == 0) || (this.Header.CheckProofOfWork() && (this.Header.Bits == GetWorkRequired(consensus)));
        }

        /// <summary>
        /// Find first common block between two chains.
        /// </summary>
        /// <param name="block">The tip of the other chain.</param>
        /// <returns>First common block or <c>null</c>.</returns>
        public ChainedHeader FindFork(ChainedHeader block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            ChainedHeader highChain = this.Height > block.Height ? this : block;
            ChainedHeader lowChain = highChain == this ? block : this;

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
        public ChainedHeader GetAncestor(int ancestorHeight)
        {
            if (ancestorHeight > this.Height)
                return null;

            ChainedHeader walk = this;
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
        /// Select all headers between current header and <paramref name="chainedHeader"/> and add them to an array
        /// of consecutive headers, both items are included in the array.
        /// </summary>
        /// <returns>Array of consecutive headers.</returns>
        public ChainedHeader[] ToChainedHeaderArray(ChainedHeader chainedHeader)
        {
            var hashes = new ChainedHeader[this.Height - chainedHeader.Height + 1];

            ChainedHeader currentHeader = this;

            for (int i = hashes.Length - 1; i >= 0; i--)
            {
                hashes[i] = currentHeader;
                currentHeader = currentHeader.Previous;
            }

            if (hashes[0] != chainedHeader)
                throw new NotSupportedException("Header must be on the same chain.");

            return hashes;
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
            return (height & 1) != 0 ? InvertLowestOne(InvertLowestOne(height - 1)) + 1 : InvertLowestOne(height);
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

        /// <summary>
        /// Replace the <see cref="BlockHeader"/> with a new provided header.
        /// </summary>
        /// <param name="newHeader">The new header to set.</param>
        /// <remarks>Use this method very carefully because it could cause race conditions if used at the wrong moment.</remarks>
        public void SetHeader(BlockHeader newHeader)
        {
            this.Header = newHeader;
        }
    }
}
