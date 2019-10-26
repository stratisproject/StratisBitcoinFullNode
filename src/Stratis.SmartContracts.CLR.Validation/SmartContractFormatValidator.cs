using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates the format of a Smart Contract <see cref="ModuleDefinition"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            ValidationPolicy policy = FormatPolicy.Default;

            var validator = new ModulePolicyValidator(policy);

            List<ValidationResult> results = validator.Validate(moduleDefinition).ToList();

            return new SmartContractValidationResult(results);
        }
    }
}