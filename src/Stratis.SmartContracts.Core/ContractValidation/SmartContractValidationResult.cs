using System.Collections.Generic;
using System.Linq;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidationResult
    {
        public IEnumerable<ValidationResult> Errors { get; private set; }

        public bool IsValid { get { return !this.Errors.Any(); } }

        public SmartContractValidationResult(IEnumerable<ValidationResult> errors)
        {
            this.Errors = errors;
        }
    }
}