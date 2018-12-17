using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.RestClients;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    public class BlockObserver : SignalObserver<ChainedHeaderBlock>
    {
        // The monitor we pass the new blocks onto.
        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly IFederationGatewayClient federationGatewayClient;

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly IDepositExtractor depositExtractor;

        private readonly IWithdrawalExtractor withdrawalExtractor;

        private readonly IWithdrawalReceiver withdrawalReceiver;

        /// <summary>
        /// Initialize the block observer with the wallet manager and the cross chain monitor.
        /// </summary>
        /// <param name="walletSyncManager">The wallet sync manager to pass new incoming blocks to.</param>
        /// <param name="depositExtractor">The component used to extract the deposits from the blocks appearing on chain.</param>
        /// <param name="withdrawalExtractor">The component used to extract withdrawals from blocks.</param>
        /// <param name="withdrawalReceiver">The component that receives the withdrawals extracted from blocks.</param>
        /// <param name="federationGatewayClient">Client for federation gateway api.</param>
        /// <param name="blockTipSender">Service responsible for publishing the block tip.</param>
        public BlockObserver(IFederationWalletSyncManager walletSyncManager,
                             IDepositExtractor depositExtractor,
                             IWithdrawalExtractor withdrawalExtractor,
                             IWithdrawalReceiver withdrawalReceiver,
                             IFederationGatewayClient federationGatewayClient,
                             IMaturedBlocksProvider maturedBlocksProvider)
        {
            Guard.NotNull(walletSyncManager, nameof(walletSyncManager));
            Guard.NotNull(federationGatewayClient, nameof(federationGatewayClient));
            Guard.NotNull(maturedBlocksProvider, nameof(maturedBlocksProvider));
            Guard.NotNull(depositExtractor, nameof(depositExtractor));
            Guard.NotNull(withdrawalExtractor, nameof(withdrawalExtractor));
            Guard.NotNull(withdrawalReceiver, nameof(withdrawalReceiver));

            this.walletSyncManager = walletSyncManager;
            this.federationGatewayClient = federationGatewayClient;
            this.maturedBlocksProvider = maturedBlocksProvider;
            this.depositExtractor = depositExtractor;
            this.withdrawalExtractor = withdrawalExtractor;
            this.withdrawalReceiver = withdrawalReceiver;
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="chainedHeaderBlock">The new block.</param>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.walletSyncManager.ProcessBlock(chainedHeaderBlock.Block);

            this.federationGatewayClient.PushCurrentBlockTipAsync(
                new BlockTipModel(
                    chainedHeaderBlock.ChainedHeader.HashBlock,
                    chainedHeaderBlock.ChainedHeader.Height,
                    (int)this.depositExtractor.MinimumDepositConfirmations)).ConfigureAwait(false).GetAwaiter().GetResult();

            IReadOnlyList<IWithdrawal> withdrawals = this.withdrawalExtractor.ExtractWithdrawalsFromBlock(
                chainedHeaderBlock.Block,
                chainedHeaderBlock.ChainedHeader.Height);

            this.withdrawalReceiver.ReceiveWithdrawals(withdrawals);

            IMaturedBlockDeposits maturedBlockDeposits = this.maturedBlocksProvider.ExtractMaturedBlockDeposits(chainedHeaderBlock.ChainedHeader);

            if (maturedBlockDeposits == null)
                return;

            // TODO remove this ugly cast: (MaturedBlockDepositsModel)maturedBlockDeposits
            this.federationGatewayClient.PushMaturedBlockAsync((MaturedBlockDepositsModel)maturedBlockDeposits).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}