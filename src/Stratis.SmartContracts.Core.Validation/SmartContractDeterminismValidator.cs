using System.Collections.Generic;
using System.Linq;
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
        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var policy = DeterminismPolicyFactory.CreatePolicy();
            var validator = new TypePolicyValidator(policy);
            IEnumerable<TypeDefinition> contractTypes = decompilation.ModuleDefinition.GetDevelopedTypes();
            var validationResults = new List<ValidationResult>();

            foreach(TypeDefinition contractType in contractTypes)
            {
                validationResults.AddRange(validator.Validate(contractType));
            }

            return new SmartContractValidationResult(validationResults);
        }
    }
}