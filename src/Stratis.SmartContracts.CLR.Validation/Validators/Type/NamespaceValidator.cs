using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have a namespace
    /// </summary>
    public class NamespaceValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {            
            if (type != null && type.Namespace != "")
            {
                return new []
                {
                    new TypeDefinitionValidationResult("Class must not have a namespace.")
                };
            }

            return Enumerable.Empty<TypeDefinitionValidationResult>();
        }
    }
}