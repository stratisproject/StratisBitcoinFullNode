using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    /// <summary>
    /// Provides block data for the given block hashes.
    /// </summary>
    /// <remarks>
    /// First we check if the block exists in chained header tree, then it check the block store and if it wasn't found there the block will be scheduled for download.
    /// Given callback is called when the block is obtained. If obtaining the block fails the callback will be called with <c>null</c>.
    /// </remarks>
    public sealed class DownloadBlocksVisitor : IConsensusVisitor<DownloadBlocksVisitorResult>
    {
        /// <summary>The block hashes to download.</summary>
        public List<uint256> BlockHashes { get; set; }

        private readonly ILogger logger;

        /// <summary>The callback that will be called for each downloaded block.</summary>
        public OnBlockDownloadedCallback OnBlockDownloadedCallback { get; set; }

        public bool TriggerDownload { get; set; }

        public DownloadBlocksVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.TriggerDownload = true;
        }

        /// <inheritdoc/>
        public async Task<DownloadBlocksVisitorResult> VisitAsync(ConsensusManager consensusManager)
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

            return new DownloadBlocksVisitorResult();
        }
    }

    public class DownloadBlocksVisitorResult { }
}
