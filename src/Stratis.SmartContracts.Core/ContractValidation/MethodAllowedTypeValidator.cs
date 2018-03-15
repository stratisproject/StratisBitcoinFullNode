using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public class MethodAllowedTypeValidator : IMethodDefinitionValidator
    {
        public static string ErrorType = "Known Non-Deterministic Method";

        private static readonly HashSet<string> RedLightTypes = new HashSet<string>
        {
            "System.Threading",
            "System.AppDomain",
            "System.Environment"
        };

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            if (RedLightTypes.Contains(method.DeclaringType.FullName))
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        ErrorType,
                        $"Use of {method.DeclaringType.FullName} is non-deterministic [{ErrorType}]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}