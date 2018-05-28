using System.Collections.Generic;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public sealed class SmartContractValidator : ISmartContractValidator
    {
        private readonly IList<ISmartContractValidator> validators;
        private readonly IEnumerable<IModuleDefinitionValidator> moduleDefinitionValidators;

        public SmartContractValidator(IList<ISmartContractValidator> validators)
        {
            this.validators = validators;
        }

        public SmartContractValidator(IEnumerable<IModuleDefinitionValidator> moduleDefinitionValidators)
        {
            this.moduleDefinitionValidators = moduleDefinitionValidators;
        }

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<ValidationResult>();

            foreach (ISmartContractValidator validator in this.validators)
            {
                SmartContractValidationResult result = validator.Validate(decompilation);
                errors.AddRange(result.Errors);
            }

            return new SmartContractValidationResult(errors);
        }
    }
}