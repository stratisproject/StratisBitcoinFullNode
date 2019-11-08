namespace Stratis.Bitcoin.Features.Wallet.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Models;

    public interface IWalletService
    {
        Task<AddressBalanceModel> GetReceivedByAddress(string address, CancellationToken cancellationToken = default);

        Task<WalletBalanceModel> GetBalance(string requestWalletName, string requestAccountName,
            bool requestIncludeBalanceByAddress, CancellationToken cancellationToken);

        Task<WalletHistoryModel> GetHistory(WalletHistoryRequest request, CancellationToken cancellationToken);
        Task<WalletGeneralInfoModel> GetWalletGeneralInfo(string walletName, CancellationToken cancellationToken);

        Task<WalletStatsModel> GetWalletStats(WalletStatsRequest request, CancellationToken cancellationToken);

        Task<WalletSendTransactionModel> SplitCoins(SplitCoinsRequest request, CancellationToken cancellationToken);
    }
}