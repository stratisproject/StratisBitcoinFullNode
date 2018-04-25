using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have a namespace
    /// </summary>
    public class NamespaceValidator : ITypeDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(TypeDefinition type)
        {            
            if (type != null && type.Namespace != "")
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError("Class must not have a namespace.")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}