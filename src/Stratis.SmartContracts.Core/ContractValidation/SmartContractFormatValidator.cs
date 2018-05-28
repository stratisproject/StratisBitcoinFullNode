using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
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

        public ValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            var errors = new List<FormatValidationError>();

            foreach (IModuleDefinitionValidator moduleDefValidator in ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(moduleDefinition));
            }

            var contractType = moduleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            foreach (ITypeDefinitionValidator typeDefValidator in TypeDefinitionValidators)
            {
                errors.AddRange(typeDefValidator.Validate(contractType));
            }

            return new ValidationResult(errors);
        }  
    }
}
