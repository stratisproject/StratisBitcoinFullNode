namespace Stratis.SmartContracts.Core.Exceptions
{
    public class DisallowedAssemblyReferenceException : StratisCompilationException
    {
        public DisallowedAssemblyReferenceException() { }
        public DisallowedAssemblyReferenceException(string message) : base(message) { }
    }
}
