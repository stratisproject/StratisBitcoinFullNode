using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
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

        public IEnumerable<FormatValidationError> Validate(MethodDefinition method)
        {
            if (RedLightTypes.Contains(method.DeclaringType.FullName))
            {
                return new List<FormatValidationError>
                {
                    new FormatValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.DeclaringType.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}