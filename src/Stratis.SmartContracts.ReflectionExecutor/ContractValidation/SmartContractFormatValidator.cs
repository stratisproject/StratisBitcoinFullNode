using System.Collections.Generic;
using Stratis.SmartContracts.ReflectionExecutor.Compilation;

namespace Stratis.SmartContracts.ReflectionExecutor.ContractValidation
{
    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        private static readonly IEnumerable<IModuleDefinitionValidator> ModuleDefinitionValidators = new List<IModuleDefinitionValidator>
        {
            new AssemblyReferenceValidator(),
            new SingleTypeValidator()
        };

        private static readonly IEnumerable<ITypeDefinitionValidator> TypeDefinitionValidators = new List<ITypeDefinitionValidator>
        {
            new NestedTypeValidator(),
            new NamespaceValidator(),
            new InheritsSmartContractValidator(),
            new SingleConstructorValidator(),
            new ConstructorParamValidator(),
            new AsyncValidator()
        };

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<SmartContractValidationError>();

            foreach (IModuleDefinitionValidator moduleDefValidator in ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(decompilation.ModuleDefinition));
            }

            foreach (ITypeDefinitionValidator typeDefValidator in TypeDefinitionValidators)
            {
                errors.AddRange(typeDefValidator.Validate(decompilation.ContractType));
            }

            return new SmartContractValidationResult(errors);
        }  
    }
}
