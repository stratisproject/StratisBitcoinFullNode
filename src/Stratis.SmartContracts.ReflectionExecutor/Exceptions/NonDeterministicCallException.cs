namespace Stratis.SmartContracts.ReflectionExecutor.Exceptions
{
    public class NonDeterministicCallException : StratisCompilationException
    {
        public NonDeterministicCallException() { }
        public NonDeterministicCallException(string message) : base(message) { }
    }
}