using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Determinism
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not PInvokeImpl
    /// </summary>
    public class PInvokeImplFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "PInvokeImpl Flag Set";

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsPInvokeImpl;

            if (invalid)
            {
                return new []
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