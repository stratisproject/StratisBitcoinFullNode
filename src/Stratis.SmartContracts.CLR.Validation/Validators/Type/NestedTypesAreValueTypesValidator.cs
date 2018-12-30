using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have top-level nested reference Types
    /// </summary>
    public class NestedTypesAreValueTypesValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (type.HasNestedTypes)
            {
                if (HasForbiddenNestedTypes(type.NestedTypes))
                {
                    return new []
                    {
                        new NestedTypeIsValueTypeValidationResult(
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

        public class NestedTypeIsValueTypeValidationResult : ValidationResult
        {
            public NestedTypeIsValueTypeValidationResult(string message) 
                : base(message)
            {
            }
        }
    }
}