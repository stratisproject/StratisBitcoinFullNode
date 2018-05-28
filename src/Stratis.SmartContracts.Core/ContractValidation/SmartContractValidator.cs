using System.Collections.Generic;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidator
    {
        private readonly IList<ISmartContractValidator> validators;

        public SmartContractValidator(IList<ISmartContractValidator> validators)
        {
            this.validators = validators;
        }

        public ValidationResult ValidateContract(SmartContractDecompilation decompilation)
        {
            var endResult = new ValidationResult();
            foreach (ISmartContractValidator validator in this.validators)
            {
                ValidationResult result = validator.Validate(decompilation);
                endResult.Errors.AddRange(result.Errors);
            }
            return endResult;
        }
    }
}