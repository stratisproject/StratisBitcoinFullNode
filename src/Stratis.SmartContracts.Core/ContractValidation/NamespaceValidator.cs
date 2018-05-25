using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have a namespace
    /// </summary>
    public class NamespaceValidator : ITypeDefinitionValidator
    {
        public IEnumerable<FormatValidationError> Validate(TypeDefinition type)
        {            
            if (type != null && type.Namespace != "")
            {
                return new List<FormatValidationError>
                {
                    new FormatValidationError("Class must not have a namespace.")
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}