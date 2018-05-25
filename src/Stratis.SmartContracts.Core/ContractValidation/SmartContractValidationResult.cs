using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidationResult
    {
        public List<FormatValidationError> Errors { get; private set; }
        public Gas GasUnitsUsed { get; set; }
        public bool IsValid { get { return !this.Errors.Any(); } }

        public SmartContractValidationResult()
        {
            this.Errors = new List<FormatValidationError>();
        }

        public SmartContractValidationResult(List<FormatValidationError> errors)
        {
            this.Errors = errors;
        }
    }
}