using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// Validates a method and its entire method reference tree using the supplied validator
    /// </summary>
    public class MethodReferenceTreeValidator : IMethodDefinitionValidator
    {
        private readonly IReferencedMethodResolver referencedMethodResolver;
        private readonly IMethodDefinitionValidator validator;

        public MethodReferenceTreeValidator(IMethodDefinitionValidator validator, IReferencedMethodResolver referencedMethodResolver)
        {
            this.validator = validator;
            this.referencedMethodResolver = referencedMethodResolver;
        }

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            var results = new Dictionary<string, List<ValidationResult>>();

            var methodsToValidate = new Stack<MethodDefinition>();

            methodsToValidate.Push(method);

            while (true)
            {
                // If there are no methods here we're done already
                if (methodsToValidate.Count == 0)
                    break;

                // Pop the next method off and validate it
                MethodDefinition next = methodsToValidate.Pop();

                foreach (MethodDefinition referencedMethod in this.referencedMethodResolver.GetReferencedMethods(next))
                {
                    // Don't add if we've already validated this method
                    if (results.ContainsKey(referencedMethod.FullName))
                    {
                        continue;
                    }

                    methodsToValidate.Push(referencedMethod);
                }

                results[next.FullName] = this.validator.Validate(next).ToList();
            }

            // Condense results
            return results.Values.SelectMany(v => v);
        }
    }
}