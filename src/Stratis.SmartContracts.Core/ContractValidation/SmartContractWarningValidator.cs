using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
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

        public ValidationResult Validate(SmartContractDecompilation decompilation)
        {
            var warnings = new List<FormatValidationError>();
            var contractType = decompilation.ModuleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            foreach (ITypeDefinitionValidator typeDefinitionValidator in TypeDefinitionValidators)
            {
                warnings.AddRange(typeDefinitionValidator.Validate(contractType));
            }

            return new ValidationResult(warnings);
        }
    }
}
