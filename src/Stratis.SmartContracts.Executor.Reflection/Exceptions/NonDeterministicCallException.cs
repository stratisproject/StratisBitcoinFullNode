namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public class NonDeterministicCallException : StratisCompilationException
    {
        public NonDeterministicCallException() { }
        public NonDeterministicCallException(string message) : base(message) { }
    }
}