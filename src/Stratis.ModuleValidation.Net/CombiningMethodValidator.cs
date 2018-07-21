using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// Combines the behaviour of multiple method validators into a single method validator
    /// </summary>
    public class CombiningMethodValidator : IMethodDefinitionValidator
    {
        private readonly IEnumerable<IMethodDefinitionValidator> validators;

        public CombiningMethodValidator(IEnumerable<IMethodDefinitionValidator> methodDefinitionValidators)
        {
            this.validators = methodDefinitionValidators;
        }

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            return this.validators.SelectMany(v => v.Validate(method));
        }
    }
}