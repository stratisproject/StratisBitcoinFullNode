using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates that methods don't contain any multi-dimensional array initializers.
    /// </summary>
    public class MultiDimensionalArrayValidator : IInstructionValidator
    {
        public static readonly string ErrorType = "Multi-Dimensional Arrays Not Supported";

        public IEnumerable<ValidationResult> Validate(Instruction instruction, MethodDefinition method)
        {
            if (instruction.OpCode.Code == Code.Newobj)
            {
                var methodRef = (MethodReference)instruction.Operand;

                if (methodRef.DeclaringType.IsArray && ((ArrayType)methodRef.DeclaringType).Dimensions.Count >= 2)
                {
                    return new List<ValidationResult>
                        {
                            new MethodParamValidationResult(method)
                        };
                }
            }
            return Enumerable.Empty<MethodDefinitionValidationResult>();
        }

        public class MethodParamValidationResult : MethodDefinitionValidationResult
        {
            public MethodParamValidationResult(MethodDefinition method)
                : base(method.FullName, ErrorType, $"{method.FullName} is invalid [{ErrorType}]")
            {
            }
        }
    }
}
