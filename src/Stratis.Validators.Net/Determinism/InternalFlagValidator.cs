using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Determinism
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not an internal call
    /// </summary>
    public class InternalFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Internal Flag Set";

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsInternalCall;

            if (invalid)
            {
                return new List<MethodDefinitionValidationResult>
                {
                    new MethodDefinitionValidationResult(
                        method.Name,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<MethodDefinitionValidationResult>();
        }
    }
}