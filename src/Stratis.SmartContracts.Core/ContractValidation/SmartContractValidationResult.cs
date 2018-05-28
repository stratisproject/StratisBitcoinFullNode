using System.Collections.Generic;
using System.Linq;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidationResult
    {
        public List<ValidationResult> Errors { get; private set; }

        public bool IsValid { get { return !this.Errors.Any(); } }

        public SmartContractValidationResult()
        {
            this.Errors = new List<ValidationResult>();
        }

        public SmartContractValidationResult(List<ValidationResult> errors)
        {
            this.Errors = errors;
        }
    }
}