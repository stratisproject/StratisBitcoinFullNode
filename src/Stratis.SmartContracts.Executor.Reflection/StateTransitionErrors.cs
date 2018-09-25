namespace Stratis.SmartContracts.Executor.Reflection
{
    public static class StateTransitionErrors
    {
        public const string InsufficientBalance = "Insufficient balance.";
        public const string InsufficientGas = "Insufficient gas.";
        public const string NoCode = "No code at this address.";
        public const string NoMethodName = "No method exists with the name given at this address.";
        public const string OutOfGas = "Execution ran out of gas.";
    }
}
