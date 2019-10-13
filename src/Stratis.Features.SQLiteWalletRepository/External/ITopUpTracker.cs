using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Features.SQLiteWalletRepository.External
{
    public interface ITopUpTracker
    {
        int WalletId { get; }
        int AccountIndex { get; }
        int AddressType { get; }
        int AddressCount { get; }
        int NextAddressIndex { get; }
        bool IsWatchOnlyAccount { get; }

        AddressIdentifier CreateAddress();
    }
}
