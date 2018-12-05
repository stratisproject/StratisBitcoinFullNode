using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> contains a static constructor
    /// </summary>
    public class StaticConstructorValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            return type.GetStaticConstructor() != null 
                ? new[] { new StaticConstructorValidationResult(type)} 
                : Enumerable.Empty<ValidationResult>();
        }

        public class StaticConstructorValidationResult : ValidationResult
        {
            public StaticConstructorValidationResult(TypeDefinition type) 
                : base(type.FullName, "Static constructor", $"Type {type.Name} contains a static constructor")
            {
            }
        }
    }
}