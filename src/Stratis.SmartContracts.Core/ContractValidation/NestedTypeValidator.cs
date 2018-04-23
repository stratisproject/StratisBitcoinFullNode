using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have nested types
    /// </summary>
    public class NestedTypeValidator : ITypeDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(TypeDefinition type)
        {
            if (type.HasNestedTypes)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError("Only the compilation of a single class is allowed. Includes inner types.")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}