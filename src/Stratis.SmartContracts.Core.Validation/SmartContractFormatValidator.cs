using System.Collections.Generic;
using System.Reflection;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        private static readonly List<IModuleDefinitionValidator> ModuleDefinitionValidators = new List<IModuleDefinitionValidator>
        {
            new SmartContractTypeDefinitionValidator()
        };

        public SmartContractFormatValidator(IEnumerable<Assembly> allowedAssemblies)
        {
            // TODO - Factor out allowed assemblies
            ModuleDefinitionValidators.Add(new AssemblyReferenceValidator(allowedAssemblies));
        }

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<ValidationResult>();

            foreach (IModuleDefinitionValidator moduleDefValidator in ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(decompilation.ModuleDefinition));
            }

            return new SmartContractValidationResult(errors);
        }  
    }
}
