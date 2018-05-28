using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.Validators.Net.Format
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
                    return new List<ValidationResult>
                    {
                        new ValidationResult(
                            "Only the compilation of a single class is allowed. Includes nested reference types.")
                    };
                }
            }

            return Enumerable.Empty<ValidationResult>();
        }

        private static bool HasForbiddenNestedTypes(IEnumerable<TypeDefinition> nestedTypes)
        {
            // We allow nested value types but forbid all others
            return !nestedTypes.All(n => n.IsValueType);
        }
    }
}