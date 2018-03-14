using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class SmartContractValidationResult
    {
        public List<SmartContractValidationError> Errors { get; set; }
        public bool Valid => !this.Errors.Any();

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