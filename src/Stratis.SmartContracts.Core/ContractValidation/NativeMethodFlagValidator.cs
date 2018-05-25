using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> is not native
    /// </summary>
    public class NativeMethodFlagValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Native Flag Set";

        public IEnumerable<FormatValidationError> Validate(MethodDefinition method)
        {
            // Instruction accesses external info.
            var invalid = method.IsNative;

            if (invalid)
            {
                return new List<FormatValidationError>
                {
                    new FormatValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}