using System.Collections.Generic;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates any warn-level issues with a Smart Contract
    /// </summary>
    public class SmartContractWarningValidator : IValidator
    {
        private static readonly IEnumerable<ITypeDefinitionValidator> TypeDefinitionValidators = new List<ITypeDefinitionValidator>
        {
            new FieldDefinitionValidator()
        };

        public ValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var warnings = new List<FormatValidationError>();

            foreach (ITypeDefinitionValidator typeDefinitionValidator in TypeDefinitionValidators)
            {
                warnings.AddRange(typeDefinitionValidator.Validate(decompilation.ContractType));
            }

            return new ValidationResult(warnings);
        }
    }
}
