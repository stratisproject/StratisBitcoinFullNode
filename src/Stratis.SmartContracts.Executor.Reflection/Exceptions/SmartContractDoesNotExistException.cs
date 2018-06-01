using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    /// <summary>
    /// Exception that is raised when the contract does not exist.
    /// </summary>
    public sealed class SmartContractDoesNotExistException : SmartContractException
    {
        public string ContractName { get; set; }

        public SmartContractDoesNotExistException(string contractName)
        {
            this.ContractName = contractName;
        }
    }
}