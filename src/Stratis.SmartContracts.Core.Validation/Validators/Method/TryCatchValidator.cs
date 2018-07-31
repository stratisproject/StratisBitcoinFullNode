using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates that contracts don't contain any try-catch clauses.
    /// </summary>
    public class TryCatchValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Known Non-Deterministic Method";

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (method.Body == null || !method.Body.HasExceptionHandlers)
                return Enumerable.Empty<TryCatchValidationResult>();

            return new List<ValidationResult>
            {
                new TryCatchValidationResult(method)
            };
        }

        public class TryCatchValidationResult : ValidationResult
        {
            public TryCatchValidationResult(MethodDefinition method) 
                : base(method.Name,
                    ErrorType,
                    $"Try-catch not permitted.")
            {
            }
        }
    }
}
