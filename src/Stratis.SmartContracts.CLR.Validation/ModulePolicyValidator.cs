using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validator for a <see cref="ModuleDefinition"/> using a given <see cref="ValidationPolicy"/>
    /// </summary>
    public class ModulePolicyValidator : IModuleDefinitionValidator
    {
        private readonly ValidationPolicy policy;

        private readonly TypePolicyValidator typePolicyValidator;

        public ModulePolicyValidator(ValidationPolicy policy)
        {
            this.policy = policy;
            this.typePolicyValidator = new TypePolicyValidator(policy);
        }

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            var results = new List<ValidationResult>();

            this.ValidateModule(results, module);

            // Use GetTypes to ensure we retrieve all nested types as well
            IEnumerable<TypeDefinition> types = module.GetTypes();

            foreach (TypeDefinition type in types)
            {
                results.AddRange(this.typePolicyValidator.Validate(type));
            }
            
            return results;
        }

        private void ValidateModule(List<ValidationResult> results, ModuleDefinition module)
        {
            foreach (var validator in this.policy.ModuleDefValidators)
            {
                results.AddRange(validator.Validate(module));
            }
        }
    }
}