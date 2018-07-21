using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Stratis.ModuleValidation.Net.Format
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> contains a finalizer
    /// </summary>
    public class FinalizerValidator : ITypeDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            return type.Methods.Any(m => m.Name == "Finalize")
                ? new [] { new FinalizerValidationResult(type) }
                : Enumerable.Empty<FinalizerValidationResult>();
        }

        public class FinalizerValidationResult : ValidationResult
        {
            public FinalizerValidationResult(TypeDefinition type)
                : base(type.FullName, "Finalizer", $"Type {type.Name} defines a finalizer")
            {
            }
        }
    }
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