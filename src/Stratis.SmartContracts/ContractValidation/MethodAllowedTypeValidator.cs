using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ContractValidation
{
    public class MethodAllowedTypeValidator : IMethodDefinitionValidator
    {
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
                        $"Use of {method.DeclaringType.FullName} is non-deterministic [known non-deterministic method call]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}