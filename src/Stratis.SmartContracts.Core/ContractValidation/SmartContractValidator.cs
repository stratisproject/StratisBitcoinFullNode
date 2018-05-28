using System.Collections.Generic;
using Stratis.SmartContracts.Core.Compilation;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidator
    {
        private readonly IList<IValidator> validators;

        public SmartContractValidator(IList<IValidator> validators)
        {
            this.validators = validators;
        }

        public ValidationResult ValidateContract(SmartContractDecompilation decompilation)
        {
            var endResult = new ValidationResult();
            foreach (IValidator validator in this.validators)
            {
                ValidationResult result = validator.Validate(decompilation);
                endResult.Errors.AddRange(result.Errors);
            }
            return endResult;
        }
    }
}