using System.Collections.Generic;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.Exceptions
{
    /// <summary>
    /// Exception that is raised when validation of the contract execution code fails.
    /// </summary>
    /// <remarks>TODO: We can possibly merge this with <see cref="SmartContractValidationResult"/>.</remarks>
    public sealed class SmartContractValidationException : SmartContractException
    {
        public List<FormatValidationError> Errors;

        public SmartContractValidationException(List<FormatValidationError> errors)
        {
            this.Errors = errors;
        }
    }
}