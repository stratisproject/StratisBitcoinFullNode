namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    public class DisallowedAssemblyReferenceException : StratisCompilationException
    {
        public DisallowedAssemblyReferenceException() { }
        public DisallowedAssemblyReferenceException(string message) : base(message) { }
    }
}