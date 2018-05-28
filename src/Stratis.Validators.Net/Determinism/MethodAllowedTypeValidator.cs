using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Determinism
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not declared in a blacklisted Type
    /// </summary>
    public class MethodAllowedTypeValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Known Non-Deterministic Method";

        private static readonly HashSet<string> RedLightTypes = new HashSet<string>
        {
            "System.Threading",
            "System.AppDomain",
            "System.Environment"
        };

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (RedLightTypes.Contains(method.DeclaringType.FullName))
            {
                return new List<MethodDefinitionValidationResult>
                {
                    new MethodDefinitionValidationResult(
                        method.Name,
                        ErrorType,
                        $"Use of {method.DeclaringType.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<MethodDefinitionValidationResult>();
        }
    }
}