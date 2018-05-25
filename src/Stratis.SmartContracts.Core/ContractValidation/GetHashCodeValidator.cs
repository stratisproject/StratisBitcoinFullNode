using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not <see cref="object.GetHashCode"/>
    /// </summary>
    public class GetHashCodeValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Known Non-Deterministic Method";

        public static readonly string GetHashCodeString = "System.Int32 System.Object::GetHashCode()"; // TODO: get via reflection?

        public IEnumerable<FormatValidationError> Validate(MethodDefinition method)
        {
            if (method.FullName.Equals(GetHashCodeString, StringComparison.Ordinal))
            {
                return new List<FormatValidationError>
                {
                    new FormatValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is not deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}