using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;
using Stratis.Validators.Net.Format;

namespace Stratis.SmartContracts.Core.ContractValidation
{
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

        public IEnumerable<ValidationResult> Validate(ModuleDefinition moduleDefinition)
        {
            var errors = new List<ValidationResult>();

            TypeDefinition contractType = moduleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            foreach (ITypeDefinitionValidator typeDefValidator in TypeDefinitionValidators)
            {
                errors.AddRange(typeDefValidator.Validate(contractType));
            }

            return errors;
        }
    }
}