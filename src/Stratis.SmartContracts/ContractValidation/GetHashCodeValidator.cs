using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ContractValidation
{
    public class GetHashCodeValidator : IMethodDefinitionValidator
    {
        public static readonly string GetHashCodeString = "System.Int32 System.Object::GetHashCode()";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            if (method.FullName.Equals(GetHashCodeString, StringComparison.Ordinal))
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        $"Use of {method.FullName} is not deterministic [known non-deterministic method call]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}