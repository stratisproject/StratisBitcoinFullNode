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
        List<WithdrawalModel> GetPending();
    }

    public class WithdrawalHistoryProvider : IWithdrawalHistoryProvider
    {
        private readonly Network network;
        private readonly IFederatedPegSettings federatedPegSettings;
        private readonly IFederationWalletManager federationWalletManager;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly MempoolManager mempoolManager;

        /// <summary>
        /// The <see cref="WithdrawalHistoryProvider"/> constructor.
        /// </summary>
        /// <param name="network">Network we are running on.</param>
        /// <param name="federatedPegSettings">Federation settings providing access to number of signatures required.</param>
        /// <param name="federationWalletManager">Wallet manager which provides access to the wallet.</param>
        /// <param name="crossChainTransferStore">Store which provides access to the statuses.</param>
        /// <param name="mempoolManager">Mempool which provides information about transactions in the mempool.</param>
        public WithdrawalHistoryProvider(
            Network network,
            IFederatedPegSettings federatedPegSettings,
            IFederationWalletManager federationWalletManager,
            ICrossChainTransferStore crossChainTransferStore,
            MempoolManager mempoolManager)
        {
            this.network = network;
            this.federatedPegSettings = federatedPegSettings;
            this.federationWalletManager = federationWalletManager;
            this.crossChainTransferStore = crossChainTransferStore;
            this.mempoolManager = mempoolManager;
        }

        // TODO: These can be more efficient, i.e. remove the wallet calls from GetHistory
        // And use a different model for Withdrawals. It doesn't quite map to the Withdrawal class.

        /// <summary>
        /// Get the history of successful withdrawals.
        /// </summary>
        /// <param name="maximumEntriesToReturn">The maximum number of entries to return.</param>
        /// <returns>A <see cref="WithdrawalModel"/> object containing a history of withdrawals.</returns>
        public List<WithdrawalModel> GetHistory(int maximumEntriesToReturn)
        {
            var result = new List<WithdrawalModel>();

            // Enumerate withdrawals starting with the most recent.
            IWithdrawal[] withdrawals = this.federationWalletManager.FindWithdrawalTransactions(sort: true)
                .Select(w => w.Item2)
                .Where(x => x.BlockHash != null)
                .Reverse()
                .Take(maximumEntriesToReturn)
                .ToArray();

            if (withdrawals.Length > 0)
            {
                ICrossChainTransfer[] transfers = this.crossChainTransferStore.QueryTransfersById(withdrawals.Select(w => w.DepositId).ToArray());

                for (int i = 0; i < withdrawals.Length; i++)
                {
                    ICrossChainTransfer transfer = transfers[i];
                    var model = new WithdrawalModel();
                    model.withdrawal = withdrawals[i];
                    model.TransferStatus = transfer?.Status.ToString();
                    result.Add(model);
                }
            }

            return result;
        }

        /// <summary>
        /// Get pending withdrawals.
        /// </summary>
        /// <returns>A <see cref="WithdrawalModel"/> object containing pending withdrawals and statuses.</returns>
        public List<WithdrawalModel> GetPending()
        {
            var result = new List<WithdrawalModel>();

            // Get all Suspended, all Partial, and all FullySigned transfers.
            ICrossChainTransfer[] inProgressTransfers = this.crossChainTransferStore.QueryTransfersByStatus(new CrossChainTransferStatus[]
            {
                CrossChainTransferStatus.Suspended,
                CrossChainTransferStatus.Partial,
                CrossChainTransferStatus.FullySigned
            });

            foreach (ICrossChainTransfer transfer in inProgressTransfers)
            {
                var model = new WithdrawalModel();
                model.withdrawal = new Withdrawal(
                    transfer.DepositTransactionId,
                    transfer.PartialTransaction?.GetHash(),
                    transfer.DepositAmount,
                    transfer.DepositTargetAddress.GetDestinationAddress(this.network).ToString(),
                    transfer.BlockHeight ?? 0,
                    transfer.BlockHash
                    );

                string status = transfer?.Status.ToString();
                switch (transfer?.Status)
                {
                    case CrossChainTransferStatus.FullySigned:
                        if (this.mempoolManager.InfoAsync(model.withdrawal.Id).GetAwaiter().GetResult() != null)
                            status += "+InMempool";

                        model.SpendingOutputDetails = this.GetSpendingInfo(transfer.PartialTransaction);
                        break;
                    case CrossChainTransferStatus.Partial:
                        status += " (" + transfer.GetSignatureCount(this.network) + "/" + this.federatedPegSettings.MultiSigM + ")";
                        model.SpendingOutputDetails = this.GetSpendingInfo(transfer.PartialTransaction);
                        break;
                }

                model.TransferStatus = status;

                result.Add(model);
            }

            return result;
        }

        private string GetSpendingInfo(Transaction partialTransaction)
        {
            string ret = "";

            foreach (TxIn input in partialTransaction.Inputs)
            {
                ret += input.PrevOut.Hash.ToString().Substring(0, 6) + "-" + input.PrevOut.N + ",";
            }

            return ret;
        }
    }
}
