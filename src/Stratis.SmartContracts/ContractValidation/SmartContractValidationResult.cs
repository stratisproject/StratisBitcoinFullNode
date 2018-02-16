using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractValidationResult
    {
        public List<SmartContractValidationError> Errors { get; set; }
        public bool Valid => !Errors.Any();

        public SmartContractValidationResult()
        {
            Errors = new List<SmartContractValidationError>();
        }

        public SmartContractValidationResult(List<SmartContractValidationError> errors)
        {
            Errors = errors;
        }
    }
}