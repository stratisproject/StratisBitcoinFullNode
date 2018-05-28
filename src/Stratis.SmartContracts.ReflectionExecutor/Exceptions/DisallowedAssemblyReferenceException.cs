namespace Stratis.SmartContracts.ReflectionExecutor.Exceptions
{
    public class DisallowedAssemblyReferenceException : StratisCompilationException
    {
        public DisallowedAssemblyReferenceException() { }
        public DisallowedAssemblyReferenceException(string message) : base(message) { }
    }
}