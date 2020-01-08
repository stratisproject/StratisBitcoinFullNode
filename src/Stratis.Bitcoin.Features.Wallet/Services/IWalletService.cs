using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.Wallet.Services
{
    public interface IWalletService
    {
        Task<IEnumerable<string>> GetWalletNames(CancellationToken cancellationToken);

        Task<string> CreateWallet(WalletCreationRequest request, CancellationToken cancellationToken);

        Task<AddressBalanceModel> GetReceivedByAddress(string address, CancellationToken cancellationToken);

        Task<WalletBalanceModel> GetBalance(string requestWalletName, string requestAccountName,
            bool requestIncludeBalanceByAddress, CancellationToken cancellationToken);

        Task<WalletHistoryModel> GetHistory(WalletHistoryRequest request, CancellationToken cancellationToken);
        Task<WalletGeneralInfoModel> GetWalletGeneralInfo(string walletName, CancellationToken cancellationToken);

        Task<WalletStatsModel> GetWalletStats(WalletStatsRequest request, CancellationToken cancellationToken);

        Task<WalletSendTransactionModel> SplitCoins(SplitCoinsRequest request, CancellationToken cancellationToken);

        Task<WalletSendTransactionModel> SendTransaction(SendTransactionRequest request,
            CancellationToken cancellationToken);

        Task<IEnumerable<RemovedTransactionModel>> RemoveTransactions(RemoveTransactionsModel request,
            CancellationToken cancellationToken);

        Task<AddressesModel> GetAllAddresses(GetAllAddressesModel request, CancellationToken cancellationToken);

        Task<WalletBuildTransactionModel> BuildTransaction(BuildTransactionRequest request,
            CancellationToken cancellationToken);

        Task<Money> GetTransactionFeeEstimate(TxFeeEstimateRequest request, CancellationToken cancellationToken);

        Task RecoverViaExtPubKey(WalletExtPubRecoveryRequest request, CancellationToken token);

        Task RecoverWallet(WalletRecoveryRequest request, CancellationToken cancellationToken);

        Task LoadWallet(WalletLoadRequest request, CancellationToken cancellationToken);

        Task<MaxSpendableAmountModel> GetMaximumSpendableBalance(WalletMaximumBalanceRequest request,
            CancellationToken cancellationToken);

        Task<SpendableTransactionsModel> GetSpendableTransactions(SpendableTransactionsRequest request,
            CancellationToken cancellationToken);
    }
}