using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Format
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have nested types
    /// </summary>
    public class NestedTypeValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (type.HasNestedTypes)
            {
                if (HasForbiddenNestedTypes(type.NestedTypes))
                {
                    return new []
                    {
                        new TypeDefinitionValidationResult(
                            "Only the compilation of a single class is allowed. Includes nested reference types.")
                    };
                }
            }

            return Enumerable.Empty<TypeDefinitionValidationResult>();
        }

        private static bool HasForbiddenNestedTypes(IEnumerable<TypeDefinition> nestedTypes)
        {
            // We allow nested value types but forbid all others
            return !nestedTypes.All(n => n.IsValueType);
        }
    }
}