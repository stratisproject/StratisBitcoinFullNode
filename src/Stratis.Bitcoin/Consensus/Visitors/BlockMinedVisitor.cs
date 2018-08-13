using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.ValidationResults;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    /// <summary>
    /// A new block was mined by the node and is attempting to connect to the tip.
    /// </summary>
    public sealed class BlockMinedConsensusVisitor : IConsensusVisitor<BlockMinedConsensusVisitorResult>
    {
        /// <summary>
        /// The block that was mined or staked by the node.
        /// </summary>
        private readonly Block block;
        private readonly ILogger logger;

        public BlockMinedConsensusVisitor(ILoggerFactory loggerFactory, Block block)
        {
            this.block = block;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <inheritdoc/>
        public async Task<BlockMinedConsensusVisitorResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.block), this.block.GetHash());

            PartialValidationResult partialValidationResult;

            using (await consensusManager.ReorgLock.LockAsync().ConfigureAwait(false))
            {
                ChainedHeader chainedHeader;

                lock (consensusManager.PeerLock)
                {
                    if (this.block.Header.HashPrevBlock != consensusManager.Tip.HashBlock)
                    {
                        this.logger.LogTrace("(-)[BLOCKMINED_INVALID_PREVIOUS_TIP]:null");
                        return null;
                    }

                    chainedHeader = consensusManager.ChainedHeaderTree.CreateChainedHeaderWithBlock(this.block);
                }

                partialValidationResult = await consensusManager.PartialValidator.ValidateAsync(this.block, chainedHeader).ConfigureAwait(false);
                if (partialValidationResult.Succeeded)
                {
                    bool fullValidationRequired;

                    lock (consensusManager.PeerLock)
                    {
                        consensusManager.ChainedHeaderTree.PartialValidationSucceeded(chainedHeader, out fullValidationRequired);
                    }

                    if (fullValidationRequired)
                    {
                        ConnectBlocksResult fullValidationResult = await consensusManager.FullyValidateLockedAsync(partialValidationResult.ChainedHeaderBlock).ConfigureAwait(false);
                        if (!fullValidationResult.Succeeded)
                        {
                            lock (consensusManager.PeerLock)
                            {
                                consensusManager.ChainedHeaderTree.PartialOrFullValidationFailed(chainedHeader);
                            }

                            this.logger.LogTrace("Miner produced an invalid block, full validation failed: {0}", fullValidationResult.Error.Message);
                            this.logger.LogTrace("(-)[FULL_VALIDATION_FAILED]");
                            throw new ConsensusException(fullValidationResult.Error.Message);
                        }
                    }
                    else
                    {
                        this.logger.LogTrace("(-)[FULL_VALIDATION_WAS_NOT_REQUIRED]");
                        throw new ConsensusException("Full validation was not required.");
                    }
                }
                else
                {
                    lock (consensusManager.PeerLock)
                    {
                        consensusManager.ChainedHeaderTree.PartialOrFullValidationFailed(chainedHeader);
                    }

                    this.logger.LogError("Miner produced an invalid block, partial validation failed: {0}", partialValidationResult.Error.Message);
                    this.logger.LogTrace("(-)[PARTIAL_VALIDATION_FAILED]");
                    throw new ConsensusException(partialValidationResult.Error.Message);
                }
            }

            this.logger.LogTrace("(-):{0}", partialValidationResult.ChainedHeaderBlock);
            return new BlockMinedConsensusVisitorResult(partialValidationResult.ChainedHeaderBlock);
        }
    }

    public class BlockMinedConsensusVisitorResult
    {
        public BlockMinedConsensusVisitorResult(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.ChainedHeaderBlock = chainedHeaderBlock;
        }

        public ChainedHeaderBlock ChainedHeaderBlock { get; private set; }
    }
}
