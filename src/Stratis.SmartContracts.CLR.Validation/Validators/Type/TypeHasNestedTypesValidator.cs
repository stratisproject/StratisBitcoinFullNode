using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="TypeDefinition"/> contains nested types
    /// </summary>
    public class TypeHasNestedTypesValidator : ITypeDefinitionValidator 
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (type.HasNestedTypes)
            {
                return new[]
                {
                    new TypeHasNestedTypesValidationResult(type)
                };
            }

            return Enumerable.Empty<ValidationResult>();
        }

        public class TypeHasNestedTypesValidationResult : ValidationResult
        {
            public TypeHasNestedTypesValidationResult(TypeDefinition type)
                : base(type.FullName, "Nested Type", $"{type.FullName} has nested types")
            {
            }
        }
    }
}