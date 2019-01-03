using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates that <see cref="Mono.Cecil.TypeDefinition"/> inherits from <see cref="Stratis.SmartContracts.SmartContract"/>
    /// </summary>
    public class InheritsSmartContractValidator : ITypeDefinitionValidator
    {
        public static string SmartContractType = typeof(SmartContract).FullName;

        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (type.BaseType == null || SmartContractType != type.BaseType.FullName)
            {
                return new [] {
                    new TypeDefinitionValidationResult("Contract must implement the SmartContract class.")
                };
            }

            return Enumerable.Empty<TypeDefinitionValidationResult>();
        }
    }
}