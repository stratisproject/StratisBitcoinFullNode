using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.CLR.Validation
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