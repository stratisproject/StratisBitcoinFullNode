namespace Stratis.SmartContracts.Core.Exceptions
{
    /// <summary>
    /// Exception that is raised when validation of the contract execution code fails.
    /// </summary>
    public sealed class StandardTokenValidationException : SmartContractException
    {
        public StandardTokenValidationException(string message) : base(message)
        {
        }
    }
}