using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that <see cref="Mono.Cecil.TypeDefinition"/> inherits from <see cref="Stratis.SmartContracts.SmartContract"/>
    /// </summary>
    public class InheritsSmartContractValidator : ITypeDefinitionValidator
    {
        public static string SmartContractType = typeof(SmartContract).FullName;

        public IEnumerable<SmartContractValidationError> Validate(TypeDefinition type)
        {
            if (SmartContractType != type.BaseType.FullName)
            {
                return new List<SmartContractValidationError>{
                    new SmartContractValidationError("Contract must implement the SmartContract class.")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}