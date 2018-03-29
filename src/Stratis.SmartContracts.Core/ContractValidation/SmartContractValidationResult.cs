using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidationResult
    {
        public List<SmartContractValidationError> Errors { get; private set; }
        public Gas GasUnitsUsed { get; set; }
        public bool IsValid { get { return !this.Errors.Any(); } }

        public SmartContractValidationResult()
        {
            this.Errors = new List<SmartContractValidationError>();
        }

        public SmartContractValidationResult(List<SmartContractValidationError> errors)
        {
            this.Errors = errors;
        }
    }
}