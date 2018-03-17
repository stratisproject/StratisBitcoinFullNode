namespace Stratis.SmartContracts.Core.Exceptions
{
    public class NonDeterministicCallException : StratisCompilationException
    {
        public NonDeterministicCallException() { }
        public NonDeterministicCallException(string message) : base(message) { }
    }
}