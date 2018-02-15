using Stratis.SmartContracts;
using Stratis.SmartContracts.Exceptions;

public class ThrowRefundGasExceptionTest : SmartContract
{
    public ThrowRefundGasExceptionTest(SmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new SmartContractRefundGasException();
    }
}