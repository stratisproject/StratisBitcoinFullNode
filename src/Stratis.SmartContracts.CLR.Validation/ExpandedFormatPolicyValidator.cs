using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates the format of a smart contract class using the expanded format policy.
    /// </summary>
    public class ExpandedFormatPolicyValidator : ISmartContractValidator
    {
        public SmartContractValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            ValidationPolicy policy = ExpandedFormatPolicy.Default;

            var validator = new ModulePolicyValidator(policy);

            List<ValidationResult> results = validator.Validate(moduleDefinition).ToList();

            return new SmartContractValidationResult(results);
        }
    }
}