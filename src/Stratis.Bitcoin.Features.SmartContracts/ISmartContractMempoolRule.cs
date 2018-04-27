using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISmartContractMempoolRule
    {
        void CheckTransaction(Transaction transaction);
    }
}
