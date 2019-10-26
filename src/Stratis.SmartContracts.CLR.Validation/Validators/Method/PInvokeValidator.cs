using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Method
{
    /// <summary>
    /// Ensures that a given method is not a PInvoke.
    /// </summary>
    public class PInvokeValidator : IMethodDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.IsPInvokeImpl)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            return new List<ValidationResult>
            {
                new PInvokeValidationResult($"{methodDefinition.FullName} is invalid [contains  PInvoke]")
            };
        }

        public class PInvokeValidationResult : MethodDefinitionValidationResult
        {
            public PInvokeValidationResult(string message) : base(message)
            {
            }
        }
    }
}
