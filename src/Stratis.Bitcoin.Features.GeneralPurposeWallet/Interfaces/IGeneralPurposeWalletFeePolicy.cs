using NBitcoin;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces
{
    public interface IGeneralPurposeWalletFeePolicy
    {
        void Start();

        void Stop();

        Money GetRequiredFee(int txBytes);

        Money GetMinimumFee(int txBytes, int confirmTarget);

        Money GetMinimumFee(int txBytes, int confirmTarget, Money targetFee);

        FeeRate GetFeeRate(int confirmTarget);
    }
}
