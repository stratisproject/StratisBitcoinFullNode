using System.Collections.Generic;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Checks for non-deterministic properties inside smart contracts by validating them at the bytecode level.
    /// </summary>
    public class SmartContractDeterminismValidator : ISmartContractValidator
    {
        /// <inheritdoc/>
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            ValidationPolicy policy = DeterminismPolicy.Default;
            var validator = new ModulePolicyValidator(policy);
            IEnumerable<ValidationResult> validationResults = validator.Validate(decompilation.ModuleDefinition);
            return new SmartContractValidationResult(validationResults);
        }
    }
}