using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    public class BlockObserver : SignalObserver<ChainedHeaderBlock>
    {
        // The monitor we pass the new blocks onto.
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly IDepositExtractor depositExtractor;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IBlockStore blockStore;

        private readonly IFullNode fullNode;

        private readonly uint minimumDepositConfirmations;

        private readonly ConcurrentChain chain;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="crossChainTransactionMonitor">The crosschain transaction monitor to pass new incoming blocks to.</param>
        /// <param name="depositExtractor">The component used to extract the deposits from the blocks appearing on chain.</param>
        /// <param name="federationGatewaySettings">The settings used to run this federation node.</param>
        /// <param name="fullNode">Full node used to get rewind the chain.</param>
        /// <param name="maturedBlockSender">Service responsible for publishing newly matured blocks.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager,
                             ICrossChainTransactionMonitor crossChainTransactionMonitor,
                             IDepositExtractor depositExtractor,
                             IFederationGatewaySettings federationGatewaySettings,
                             IFullNode fullNode,
                             IMaturedBlockSender maturedBlockSender)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(crossChainTransactionMonitor, nameof(crossChainTransactionMonitor));
            Guard.NotNull(depositExtractor, nameof(depositExtractor));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(fullNode, nameof(maturedBlockSender));

            this.walletSyncManager = walletSyncManager;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.depositExtractor = depositExtractor;
            this.maturedBlockSender = maturedBlockSender;
            this.minimumDepositConfirmations = federationGatewaySettings.MinimumDepositConfirmations;
            this.chain = fullNode.NodeService<ConcurrentChain>();
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="chainedHeaderBlock">The new block.</param>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            crossChainTransactionMonitor.ProcessBlock(chainedHeaderBlock.Block);
            walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);

            // todo: persist the last seen block height in database
            // todo: save these deposits in local database
            var newlyMaturedBlock = GetNewlyMaturedBlock(chainedHeaderBlock);
            if (newlyMaturedBlock == null) return;

            var maturedBlockDeposits = ExtractMaturedBlockDeposits(newlyMaturedBlock);
            this.maturedBlockSender.SendMaturedBlockDepositsAsync(maturedBlockDeposits).ConfigureAwait(false);
        }

        private MaturedBlockDepositsModel ExtractMaturedBlockDeposits(ChainedHeader newlyMaturedBlock)
        {
            var maturedBlock =
                new MaturedBlockModel() { BlockHash = newlyMaturedBlock.HashBlock, BlockHeight = newlyMaturedBlock.Height };

            var deposits = this.depositExtractor.ExtractDepositsFromBlock(newlyMaturedBlock.Block, newlyMaturedBlock.Height);

            var maturedBlockDeposits = new MaturedBlockDepositsModel(maturedBlock, deposits);
            return maturedBlockDeposits;
        }

        private ChainedHeader GetNewlyMaturedBlock(ChainedHeaderBlock latestPublishedBlock)
        {
            var newMaturedHeight = latestPublishedBlock.ChainedHeader.Height - (int)this.minimumDepositConfirmations;
            if (newMaturedHeight < 0) return null;

            var newMaturedBlock = this.chain.GetBlock(newMaturedHeight);
            return newMaturedBlock;
        }
    }
}