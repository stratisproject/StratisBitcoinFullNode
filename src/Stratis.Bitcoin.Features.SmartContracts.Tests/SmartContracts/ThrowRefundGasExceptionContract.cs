using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.Exceptions;

public sealed class ThrowRefundGasExceptionContract : SmartContract
{
    public ThrowRefundGasExceptionContract(ISmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        SpendGas((Gas) 10);

        throw new RefundGasException();
    }
}