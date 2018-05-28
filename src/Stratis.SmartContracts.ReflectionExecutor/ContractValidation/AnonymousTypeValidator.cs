using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ReflectionExecutor.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not contain any FieldReferences to anonymous types
    /// </summary>
    public class AnonymousTypeValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Use Of Anonymous Type";

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            foreach (Mono.Cecil.Cil.Instruction instruction in method.Body.Instructions)
            {
                if (instruction.Operand is FieldReference fieldReference)
                {
                    if (fieldReference.FullName.Contains("AnonymousType"))
                    {
                        return new List<SmartContractValidationError>
                        {
                            new SmartContractValidationError(
                                method.Name,
                                method.FullName,
                                ErrorType,
                                $"{method.FullName} is invalid [{ErrorType}]")
                        };
                    }
                }
            }

            return Enumerable.Empty<SmartContractValidationError>();
        }
    }
}