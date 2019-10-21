using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    public class BlockObserver
    {
        // The monitor we pass the new blocks onto.
        private readonly IFederationWalletSyncManager federationWalletSyncManager;

        private readonly IInputConsolidator inputConsolidator;

        private readonly ISignals signals;

        private readonly SubscriptionToken blockConnectedSubscription;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        public BlockObserver(
            IFederationWalletSyncManager walletSyncManager,
            IInputConsolidator inputConsolidator,
            ISignals signals)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));

            this.federationWalletSyncManager = walletSyncManager;
            this.inputConsolidator = inputConsolidator;
            this.signals = signals;

            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(ev => this.OnBlockReceived(ev.ConnectedBlock));

            // TODO: Dispose ??
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="chainedHeaderBlock">The new block.</param>
        public void OnBlockReceived(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.federationWalletSyncManager.ProcessBlock(chainedHeaderBlock.Block);
            this.inputConsolidator.ProcessBlock(chainedHeaderBlock);
        }
    }
}