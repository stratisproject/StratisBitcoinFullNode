using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Module
{
    public class ContractToDeployValidator : IModuleDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            List<TypeDefinition> types = module.GetContractTypes().ToList();

            // Must always be at least one contract
            if (types.Count == 0)
            {
                return new[] {
                    new ContractToDeployValidationResult("Assembly must contain at least one contract type.")
                };
            }

            // Must either be one contract
            if (types.Count == 1)
                return Enumerable.Empty<ValidationResult>();

            // OR only one contract with Deploy Attribute
            if (types.Count(x => x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name)) == 1)
                return Enumerable.Empty<ValidationResult>();

            // Otherwise it's a problem
            return new[] {
                new ContractToDeployValidationResult("Assembly must contain one contract with the Deploy attribute.") 
            };
        }

        public class ContractToDeployValidationResult : ModuleDefinitionValidationResult
        {
            public ContractToDeployValidationResult(string message) : base(message)
            {
            }
        }
    }
}
