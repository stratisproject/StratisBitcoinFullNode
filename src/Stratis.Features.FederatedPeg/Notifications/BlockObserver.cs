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
        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly ISignals signals;

        private SubscriptionToken blockConnectedSubscription;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="depositExtractor">The component used to extract the deposits from the blocks appearing on chain.</param>
        /// <param name="withdrawalExtractor">The component used to extract withdrawals from blocks.</param>
        /// <param name="withdrawalReceiver">The component that receives the withdrawals extracted from blocks.</param>
        /// <param name="federationGatewayClient">Client for federation gateway api.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager, ISignals signals)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));

            this.walletSyncManager = walletSyncManager;
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
            this.walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);
        }
    }
}