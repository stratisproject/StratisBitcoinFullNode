using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// Validates all methods contained within a Type
    /// </summary>
    public class TypeMethodsValidator : ITypeDefinitionValidator
    {
        private readonly IMethodDefinitionValidator validator;

        public TypeMethodsValidator(IMethodDefinitionValidator validator)
        {
            this.validator = validator;
        }

        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            var results = new List<ValidationResult>();

            IEnumerable<MethodDefinition> methods = type.Methods;

            foreach (MethodDefinition method in methods)
            {
                IEnumerable<ValidationResult> methodValidationResults = this.validator.Validate(method);

                results.AddRange(methodValidationResults);
            }

            return results;
        }
    }
}