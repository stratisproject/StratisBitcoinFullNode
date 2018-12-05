namespace Stratis.SmartContracts.CLR
{
    public static class StateTransitionErrors
    {
        public const string InsufficientBalance = "Insufficient balance.";
        public const string InsufficientGas = "Insufficient gas.";
        public const string NoCode = "No code at this address.";
        public const string NoMethodName = "No method name was given.";
        public const string OutOfGas = "Execution ran out of gas.";
    }
}
