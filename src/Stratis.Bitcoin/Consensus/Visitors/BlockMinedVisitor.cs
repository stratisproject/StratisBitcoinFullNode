using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.ValidationResults;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class BlockMinedConsensusVisitor : IConsensusVisitor
    {
        public Block Block { get; set; }

        private readonly ILogger logger;

        public BlockMinedConsensusVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public async ConsensusVisitorResult Visit(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.Block), this.Block.GetHash());

            PartialValidationResult partialValidationResult;

            using (await consensusManager.ReorgLock.LockAsync().ConfigureAwait(false))
            {
                ChainedHeader chainedHeader;

                lock (consensusManager.PeerLock)
                {
                    if (this.Block.Header.HashPrevBlock != consensusManager.Tip.HashBlock)
                    {
                        this.logger.LogTrace("(-)[BLOCKMINED_INVALID_PREVIOUS_TIP]:null");
                        return null;
                    }

                    chainedHeader = consensusManager.ChainedHeaderTree.CreateChainedHeaderWithBlock(this.Block);
                }

                partialValidationResult = await consensusManager.PartialValidator.ValidateAsync(this.Block, chainedHeader).ConfigureAwait(false);
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
            return partialValidationResult.ChainedHeaderBlock;
        }
    }
}
