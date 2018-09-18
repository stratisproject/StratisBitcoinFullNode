namespace Stratis.SmartContracts.Executor.Reflection
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
        OverMemoryLimit
    }
}