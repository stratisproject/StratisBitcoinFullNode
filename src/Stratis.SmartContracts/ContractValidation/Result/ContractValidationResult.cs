using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.ContractValidation.Result
{
    internal class ContractValidationResult
    {
        public List<ContractValidationError> Errors { get; set; }
        public bool Valid => !Errors.Any();

        public ContractValidationResult()
        {
            Errors = new List<ContractValidationError>();
        }

        public ContractValidationResult(List<ContractValidationError> errors)
        {
            Errors = errors;
        }
    }
}
