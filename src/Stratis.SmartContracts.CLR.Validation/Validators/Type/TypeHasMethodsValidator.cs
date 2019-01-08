using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="TypeDefinition"/> does not have any methods
    /// </summary>
    public class TypeHasMethodsValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (type.HasMethods)
            {
                return new[]
                {
                    new TypeHasMethodsValidationResult(type)
                };
            }

            return Enumerable.Empty<ValidationResult>();
        }

        public class TypeHasMethodsValidationResult : ValidationResult
        {
            public TypeHasMethodsValidationResult(TypeDefinition type) 
                : base(type.FullName, "Nested Type", $"{type.FullName} has methods")
            {
            }
        }
    }
}