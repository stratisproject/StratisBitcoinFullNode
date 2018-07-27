using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Determinism
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not <see cref="object.GetHashCode"/>
    /// </summary>
    public class GetHashCodeValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Known Non-Deterministic Method";

        public static readonly string GetHashCodeString = "System.Int32 System.Object::GetHashCode()"; // TODO: get via reflection?

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (method.FullName.Equals(GetHashCodeString, StringComparison.Ordinal))
            {
                return new List<MethodDefinitionValidationResult>
                {
                    new MethodDefinitionValidationResult(
                        method.Name,
                        ErrorType,
                        $"Use of {method.FullName} is not deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<MethodDefinitionValidationResult>();
        }
    }
}