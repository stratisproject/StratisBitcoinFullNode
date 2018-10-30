using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    internal class BlockObserver : SignalObserver<ChainedHeaderBlock>
    {
        // The monitor we pass the new blocks onto.
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly IDepositExtractor depositExtractor;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="crossChainTransactionMonitor">The crosschain transaction monitor to pass new incoming blocks to.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager, 
                             ICrossChainTransactionMonitor crossChainTransactionMonitor,
                             IDepositExtractor depositExtractor)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(crossChainTransactionMonitor, nameof(crossChainTransactionMonitor));
            Guard.NotNull(depositExtractor, nameof(depositExtractor));

            this.walletSyncManager = walletSyncManager;
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
            this.depositExtractor = depositExtractor;
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="block">The new block.</param>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            crossChainTransactionMonitor.ProcessBlock(chainedHeaderBlock.Block);
            walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);

            // todo: persist the last seen block height in database
            // todo: save these deposits in local database
            var deposits = this.depositExtractor.ExtractDepositsFromBlock(
                chainedHeaderBlock.Block,
                chainedHeaderBlock.ChainedHeader.Height);
        }
    }
}