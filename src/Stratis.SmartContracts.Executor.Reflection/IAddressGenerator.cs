using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IAddressGenerator
    {
        uint160 GenerateAddress(ITransactionContext context);
    }
}