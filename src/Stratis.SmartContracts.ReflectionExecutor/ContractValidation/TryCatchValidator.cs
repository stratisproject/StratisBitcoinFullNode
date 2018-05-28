using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ReflectionExecutor.ContractValidation
{
    /// <summary>
    /// Validates that contracts don't contain any try-catch clauses.
    /// </summary>
    public class TryCatchValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Known Non-Deterministic Method";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            if (method.Body == null || !method.Body.HasExceptionHandlers)
                return Enumerable.Empty<SmartContractValidationError>();

            return new List<SmartContractValidationError>
            {
                new SmartContractValidationError(
                    method.Name,
                    method.FullName,
                    ErrorType,
                    $"Try-catch not permitted.")
            };
        }
    }
}
