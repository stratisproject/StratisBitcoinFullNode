using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.RestClients;
using Stratis.Features.FederatedPeg.TargetChain;

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

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly IDepositExtractor depositExtractor;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        private readonly ISignals signals;

        private CancellationTokenSource cancellationSource;

        private Task pushBlockTipTask;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="depositExtractor">The component used to extract the deposits from the blocks appearing on chain.</param>
        /// <param name="withdrawalExtractor">The component used to extract withdrawals from blocks.</param>
        /// <param name="withdrawalReceiver">The component that receives the withdrawals extracted from blocks.</param>
        /// <param name="federationGatewayClient">Client for federation gateway api.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager,
                             IDepositExtractor depositExtractor,
                             IWithdrawalExtractor withdrawalExtractor,
                             IWithdrawalReceiver withdrawalReceiver,
                             IFederationGatewayClient federationGatewayClient,
                             ISignals signals)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(federationGatewayClient, nameof(federationGatewayClient));
            Guard.NotNull(depositExtractor, nameof(depositExtractor));
            Guard.NotNull(withdrawalExtractor, nameof(withdrawalExtractor));
            Guard.NotNull(withdrawalReceiver, nameof(withdrawalReceiver));

            this.walletSyncManager = walletSyncManager;
            this.federationGatewayClient = federationGatewayClient;
            this.depositExtractor = depositExtractor;
            this.withdrawalExtractor = withdrawalExtractor;
            this.withdrawalReceiver = withdrawalReceiver;
            this.signals = signals;

            this.cancellationSource = null;
            this.pushBlockTipTask = null;

            this.signals.OnBlockConnected.Attach(this.OnBlockReceived);

            // TODO: Dispose with Detach ??
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="chainedHeaderBlock">The new block.</param>
        public void OnBlockReceived(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);

            // Cancel previous sending to avoid first sending new tip and then sending older tip.
            if ((this.pushBlockTipTask != null) && !this.pushBlockTipTask.IsCompleted)
            {
                this.cancellationSource.Cancel();
                this.pushBlockTipTask.GetAwaiter().GetResult();

                this.pushBlockTipTask = null;
                this.cancellationSource = null;
            }

            var blockTipModel = new BlockTipModel(chainedHeaderBlock.ChainedHeader.HashBlock,chainedHeaderBlock.ChainedHeader.Height, (int)this.depositExtractor.MinimumDepositConfirmations);

            // There is no reason to wait for the message to be sent.
            // Awaiting REST API call will only slow this callback.
            // Callbacks never supposed to do any IO calls or web requests.
            // Instead we start sending the message and if next block was connected faster than the message was sent we
            // are canceling it and sending the next tip.
            // Receiver of this message doesn't care if we are not providing tips for every block we connect,
            // it just requires to know about out latest state.
            this.cancellationSource = new CancellationTokenSource();
            this.pushBlockTipTask = Task.Run(async () => await this.federationGatewayClient.PushCurrentBlockTipAsync(blockTipModel, this.cancellationSource.Token).ConfigureAwait(false));

            IReadOnlyList<IWithdrawal> withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(
                chainedHeaderBlock.Block,
                chainedHeaderBlock.ChainedHeader.Height);

            this.withdrawalReceiver.ReceiveWithdrawals(withdrawals);
        }
    }
}