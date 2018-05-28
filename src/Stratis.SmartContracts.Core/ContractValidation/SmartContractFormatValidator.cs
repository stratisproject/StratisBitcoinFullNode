using System.Collections.Generic;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;
using Stratis.Validators.Net.Format;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : IValidator
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

        public ValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<FormatValidationError>();

            foreach (IModuleDefinitionValidator moduleDefValidator in ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(decompilation.ModuleDefinition));
            }

            foreach (ITypeDefinitionValidator typeDefValidator in TypeDefinitionValidators)
            {
                errors.AddRange(typeDefValidator.Validate(decompilation.ContractType));
            }

            return new ValidationResult(errors);
        }  
    }
}
