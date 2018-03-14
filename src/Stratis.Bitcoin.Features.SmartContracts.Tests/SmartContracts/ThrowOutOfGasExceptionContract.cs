using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.Exceptions;

public sealed class ThrowOutOfGasExceptionContract : SmartContract
{
    public ThrowOutOfGasExceptionContract(ISmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new OutOfGasException();
    }
}