using System.Collections.Generic;
using Stratis.SmartContracts.ReflectionExecutor.Compilation;

namespace Stratis.SmartContracts.ReflectionExecutor.ContractValidation
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
            var warnings = new List<SmartContractValidationError>();

            foreach (ITypeDefinitionValidator typeDefinitionValidator in TypeDefinitionValidators)
            {
                warnings.AddRange(typeDefinitionValidator.Validate(decompilation.ContractType));
            }

            return new SmartContractValidationResult(warnings);
        }
    }
}
