using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Module
{
    public class ContractToDeployValidator : IModuleDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            IEnumerable<TypeDefinition> types = module.GetDevelopedTypes().Where(x => x.BaseType.FullName == InheritsSmartContractValidator.SmartContractType);

            // Must either be one contract
            if (types.Count() == 1)
                return Enumerable.Empty<ValidationResult>();

            // OR only one contract with Deploy Attribute
            if (types.Count(x => x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name)) == 1)
                return Enumerable.Empty<ValidationResult>();

            // Otherwise it's a problem
            return new[] {
                new ContractToDeployValidationResult() 
            };
        }

        public class ContractToDeployValidationResult : ModuleDefinitionValidationResult
        {
            public ContractToDeployValidationResult() : base("Assembly must contain one contract with the Deploy attribute.")
            {
            }
        }
    }
}
