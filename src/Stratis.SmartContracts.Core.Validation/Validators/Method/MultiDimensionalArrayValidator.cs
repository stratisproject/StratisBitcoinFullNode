using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Core.Validation
{
    /// <summary>
    /// Validates that methods don't contain any 
    /// </summary>
    public class MultiDimensionalArrayValidator : IMethodDefinitionValidator
    {
        public static readonly string ErrorType = "Multi-Dimensional Arrays Not Supported";

        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (!method.HasBody || method.Body.Instructions.Count == 0)
                return Enumerable.Empty<MethodDefinitionValidationResult>();

            foreach (Instruction instruction in method.Body.Instructions)
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
