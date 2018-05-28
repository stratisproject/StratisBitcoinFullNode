using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Format
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not contain any FieldReferences to anonymous types
    /// </summary>
    public class AnonymousTypeValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Use Of Anonymous Type";

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            foreach (Mono.Cecil.Cil.Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is FieldReference fieldReference)
                {
                    if (fieldReference.FullName.Contains("AnonymousType"))
                    {
                        return new []
                        {
                            new MethodDefinitionValidationResult(
                                method.Name,
                                ErrorType,
                                $"{method.FullName} is invalid [{ErrorType}]")
                        };
                    }
                }
            }

            return Enumerable.Empty<MethodDefinitionValidationResult>();
        }
    }
}