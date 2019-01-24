using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not have top-level nested reference Types
    /// </summary>
    public class NestedTypeIsValueTypeValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            if (!type.IsValueType)
            {
                return new []
                {
                    new TypeIsValueTypeValidationResult(
                        "Only nested value types are allowed.")
                };
            }

            return Enumerable.Empty<TypeDefinitionValidationResult>();
        }

        public class TypeIsValueTypeValidationResult : ValidationResult
        {
            public TypeIsValueTypeValidationResult(string message) 
                : base(message)
            {
            }
        }
    }
}