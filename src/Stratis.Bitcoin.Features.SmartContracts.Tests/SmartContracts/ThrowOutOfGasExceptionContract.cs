using Stratis.SmartContracts;
using Stratis.SmartContracts.Exceptions;

public sealed class ThrowOutOfGasExceptionContract : SmartContract
{
    public ThrowOutOfGasExceptionContract(SmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new OutOfGasException();
    }
}