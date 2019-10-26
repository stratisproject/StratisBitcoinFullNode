namespace Stratis.SmartContracts.CLR
{
    public static class ContractInvocationErrors
    {
        public const string MethodDoesNotExist = "Method does not exist on contract.";
        public const string MethodIsConstructor = "Attempted to invoke constructor on existing contract.";
        public const string MethodIsPrivate = "Attempted to invoke private method.";
        public const string ParameterCountIncorrect = "Incorrect number of parameters passed to method.";
        public const string ParameterTypesDontMatch = "Parameters sent don't match expected method parameters.";
    }
}
