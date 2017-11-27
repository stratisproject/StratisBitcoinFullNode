using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IWalletFeePolicy
    {
        void Start();

        void Stop();

        Money GetRequiredFee(int txBytes);

        Money GetMinimumFee(int txBytes, int confirmTarget);

        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);

        FeeRate GetFeeRate(int confirmTarget);
    }
}
