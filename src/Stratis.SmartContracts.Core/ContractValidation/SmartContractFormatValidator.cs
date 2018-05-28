using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;
using Stratis.Validators.Net.Format;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class ModuleDefinitionValidator : IModuleDefinitionValidator
    {
        public ModuleDefinitionValidator(IEnumerable<IModuleDefinitionValidator> moduleDefinitionValidators)
        {
            // Use a config param here to prevent telescoping constructors
            this.ModuleDefinitionValidators = moduleDefinitionValidators;
        }

        private IEnumerable<IModuleDefinitionValidator> ModuleDefinitionValidators { get; }

        public IEnumerable<FormatValidationError> Validate(ModuleDefinition moduleDefinition)
        {
            var errors = new List<FormatValidationError>();

            foreach (IModuleDefinitionValidator moduleDefValidator in this.ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(moduleDefinition));
            }

            return errors;
        }
    }

    /// <summary>
    /// Validates the Type definitions contained within a module definition
    /// </summary>
    public class SmartContractTypeDefinitionValidator : IModuleDefinitionValidator
    {
        private static readonly IEnumerable<ITypeDefinitionValidator> TypeDefinitionValidators = new List<ITypeDefinitionValidator>
        {
            new NestedTypeValidator(),
            new NamespaceValidator(),
            new InheritsSmartContractValidator(),
            new SingleConstructorValidator(),
            new ConstructorParamValidator(),
            new AsyncValidator()
        };

        public IEnumerable<FormatValidationError> Validate(ModuleDefinition moduleDefinition)
        {
            var errors = new List<FormatValidationError>();

            TypeDefinition contractType = moduleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            foreach (ITypeDefinitionValidator typeDefValidator in TypeDefinitionValidators)
            {
                errors.AddRange(typeDefValidator.Validate(contractType));
            }

            return errors;
        }
    }

    /// <summary>
    /// Validates the format of a Smart Contract <see cref="SmartContractDecompilation"/>
    /// </summary>
    public class SmartContractFormatValidator : ISmartContractValidator
    {
        private static readonly IEnumerable<IModuleDefinitionValidator> ModuleDefinitionValidators = new List<IModuleDefinitionValidator>
        {
            new AssemblyReferenceValidator(),
            new SingleTypeValidator(),
            new SmartContractTypeDefinitionValidator()
        };

        public ValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var errors = new List<FormatValidationError>();

            foreach (IModuleDefinitionValidator moduleDefValidator in ModuleDefinitionValidators)
            {
                errors.AddRange(moduleDefValidator.Validate(decompilation.ModuleDefinition));
            }

            return new ValidationResult(errors);
        }  
    }
}
