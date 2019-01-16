using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
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

        public SmartContractValidationResult Validate(ModuleDefinition moduleDefinition)
        {
            var warnings = new List<ValidationResult>();
            IEnumerable<TypeDefinition> contractTypes = moduleDefinition.GetContractTypes();

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
