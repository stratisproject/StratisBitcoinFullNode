using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
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