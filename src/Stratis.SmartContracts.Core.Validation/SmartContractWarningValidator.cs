using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates any warn-level issues with a Smart Contract
    /// </summary>
    public class SmartContractWarningValidator : ISmartContractValidator
    {
        private static readonly IEnumerable<ITypeDefinitionValidator> TypeDefinitionValidators = new List<ITypeDefinitionValidator>
        {
            new FieldDefinitionValidator()
        };

        public SmartContractValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var warnings = new List<ValidationResult>();
            IEnumerable<TypeDefinition> contractTypes = decompilation.ModuleDefinition.GetDevelopedTypes();

            foreach(TypeDefinition contractType in contractTypes)
            {
                foreach (ITypeDefinitionValidator typeDefinitionValidator in TypeDefinitionValidators)
                {
                    warnings.AddRange(typeDefinitionValidator.Validate(contractType));
                }
            }

            return new SmartContractValidationResult(warnings);
        }
    }
}
