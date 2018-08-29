using System.Collections.Generic;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Checks for non-deterministic properties inside smart contracts by validating them at the bytecode level.
    /// </summary>
    public class SmartContractDeterminismValidator : ISmartContractValidator
    {
        /// <inheritdoc/>
        public SmartContractValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            ValidationPolicy policy = DeterminismPolicy.Default;
            var validator = new ModulePolicyValidator(policy);
            IEnumerable<ValidationResult> validationResults = validator.Validate(moduleDefinition);
            return new SmartContractValidationResult(validationResults);
        }
    }
}