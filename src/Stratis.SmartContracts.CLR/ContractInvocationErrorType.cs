namespace Stratis.SmartContracts.CLR
{
    public enum ContractInvocationErrorType
    {
        None = 0,
        MethodDoesNotExist,
        MethodIsConstructor,
        MethodIsPrivate,
        ParameterTypesDontMatch,
        ParameterCountIncorrect,
        MethodThrewException,
        OutOfGas,
        OverMemoryLimit,
        Exception
    }
}