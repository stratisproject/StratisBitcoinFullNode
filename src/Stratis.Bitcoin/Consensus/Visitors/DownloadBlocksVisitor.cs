using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class DownloadBlocksVisitor : IConsensusVisitor
    {
        public List<uint256> BlockHashes { get; set; }

        private readonly ILogger logger;

        public OnBlockDownloadedCallback OnBlockDownloadedCallback { get; set; }

        public bool TriggerDownload { get; set; }

        public DownloadBlocksVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.TriggerDownload = true;
        }

        public async Task<ConsensusVisitorResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(this.BlockHashes), nameof(this.BlockHashes.Count), this.BlockHashes.Count);

            var blocksToDownload = new List<ChainedHeader>();

            foreach (uint256 blockHash in this.BlockHashes)
            {
                ChainedHeaderBlock chainedHeaderBlock = await consensusManager.BlockLoader.LoadBlockDataAsync(consensusManager, blockHash).ConfigureAwait(false);

                if ((chainedHeaderBlock == null) || (chainedHeaderBlock.Block != null))
                {
                    if (chainedHeaderBlock != null)
                        this.logger.LogTrace("Block data loaded for hash '{0}', calling the callback.", blockHash);
                    else
                        this.logger.LogTrace("Chained header not found for hash '{0}'.", blockHash);

                    this.OnBlockDownloadedCallback(chainedHeaderBlock);
                }
                else
                {
                    blocksToDownload.Add(chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Block hash '{0}' is queued for download.", blockHash);
                }
            }

            if (blocksToDownload.Count != 0)
            {
                this.logger.LogTrace("Asking block puller for {0} blocks.", blocksToDownload.Count);
                consensusManager.BlockDownloader.DownloadBlocks(blocksToDownload.ToArray(), consensusManager.BlockDownloader.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-)");

            return new ConsensusVisitorResult();
        }
    }
}
