using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.TypeDefinition"/> does not contain a finalizer
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
}