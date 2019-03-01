using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public interface IWithdrawalHistoryProvider
    {
        List<WithdrawalModel> GetHistory(int maximumEntriesToReturn);
    }

    public class WithdrawalHistoryProvider : IWithdrawalHistoryProvider
    {
        private readonly Network network;
        private readonly IFederationGatewaySettings federationGatewaySettings;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly MempoolManager mempoolManager;

        /// <summary>
        /// The <see cref="WithdrawalHistoryProvider"/> constructor.
        /// </summary>
        /// <param name="network">Network we are running on.</param>
        /// <param name="federationGatewaySettings">Federation settings providing access to number of signatures required.</param>
        /// <param name="federationWalletManager">Wallet manager which provides access to the wallet.</param>
        /// <param name="crossChainTransferStore">Store which provides access to the statuses.</param>
        /// <param name="mempoolManager">Mempool which provides information about transactions in the mempool.</param>
        public WithdrawalHistoryProvider(
            Network network,
            IFederationGatewaySettings federationGatewaySettings,
            IFederationWalletManager federationWalletManager,
            ICrossChainTransferStore crossChainTransferStore,
            MempoolManager mempoolManager)
        {
            this.network = network;
            this.federationGatewaySettings = federationGatewaySettings;
            this.federationWalletManager = federationWalletManager;
            this.crossChainTransferStore = crossChainTransferStore;
            this.mempoolManager = mempoolManager;
        }

        /// <summary>
        /// Get the history of withdrawals and statuses.
        /// </summary>
        /// <param name="maximumEntriesToReturn">The maximum number of entries to return.</param>
        /// <returns>A <see cref="WithdrawalModel"/> object containing a history of withdrawals and statuses.</returns>
        public List<WithdrawalModel> GetHistory(int maximumEntriesToReturn)
        {
            var result = new List<WithdrawalModel>();
            IWithdrawal[] withdrawals = this.federationWalletManager.GetWithdrawals().Take(maximumEntriesToReturn).ToArray();

            if (withdrawals.Length > 0)
            {
                ICrossChainTransfer[] transfers = this.crossChainTransferStore.GetAsync(withdrawals.Select(w => w.DepositId).ToArray()).GetAwaiter().GetResult().ToArray();

                for (int i = 0; i < withdrawals.Length; i++)
                {
                    ICrossChainTransfer transfer = transfers[i];
                    var model = new WithdrawalModel();
                    model.withdrawal = withdrawals[i];
                    string status = transfer?.Status.ToString();
                    switch (transfer?.Status)
                    {
                        case CrossChainTransferStatus.FullySigned:
                            if (this.mempoolManager.InfoAsync(model.withdrawal.Id).GetAwaiter().GetResult() != null)
                                status += "+InMempool";
                            break;
                        case CrossChainTransferStatus.Partial:
                            status += " (" + transfer.GetSignatureCount(this.network) + "/" + this.federationGatewaySettings.MultiSigM + ")";
                            break;
                    }

                    model.TransferStatus = status;
                    result.Add(model);
                }
            }

            return result;
        }
    }
}
