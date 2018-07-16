using System.Collections.Generic;
using Stratis.ModuleValidation.Net;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection.Exceptions
{
    /// <summary>
    /// Exception that is raised when validation of the contract execution code fails.
    /// </summary>
    /// <remarks>TODO: We can possibly merge this with <see cref="SmartContractValidationResult"/>.</remarks>
    public sealed class SmartContractValidationException : SmartContractException
    {
        public IEnumerable<ValidationResult> Errors;

        public SmartContractValidationException(IEnumerable<ValidationResult> errors)
        {
            this.Errors = errors;
        }
    }
}