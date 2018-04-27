using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISmartContractMempoolRule
    {
        void CheckTransaction(MempoolValidationContext context);
    }
}
