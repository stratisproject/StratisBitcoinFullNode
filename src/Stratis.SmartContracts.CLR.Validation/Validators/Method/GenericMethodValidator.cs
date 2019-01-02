using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Method
{
    /// <summary>
    /// Ensures that a given method doesn't have any generic parameters.
    /// </summary>
    public class GenericMethodValidator : IMethodDefinitionValidator
    {
        public IEnumerable<ValidationResult> Validate(MethodDefinition methodDefinition)
        {
            if (!methodDefinition.HasGenericParameters)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            return new List<ValidationResult>
            {
                new GenericMethodValidationResult($"{methodDefinition.FullName} is invalid [contains generic parameter]")
            };
        }

        public class GenericMethodValidationResult : MethodDefinitionValidationResult
        {
            public GenericMethodValidationResult(string message) : base(message)
            {
            }
        }
    }
}
