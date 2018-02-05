using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ContractValidation
{
    public class MethodFlagValidator : IMethodDefinitionValidator
    {
        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {            
            // Instruction accesses external info.
            var invalid = method.IsNative || method.IsPInvokeImpl || method.IsUnmanaged || method.IsInternalCall;

            if (invalid)
            {
                return new List<SmartContractValidationError>
                {
                    new SmartContractValidationError(
                        $"Use of {method.FullName} is non-deterministic [invalid method flags]")
                };
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}