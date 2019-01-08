using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates that an instruction is not creating a new instance of an object.
    /// </summary>
    public class NewObjValidator : IInstructionValidator
    {
        public static readonly string ErrorType = "Creation Of New Objects Is Not Supported";

        public IEnumerable<ValidationResult> Validate(Instruction instruction, MethodDefinition method)
        {
            // Docs suggest that while OpCodes.Newobj can be used to create both objects and value-types,
            // OpCodes.Initobj is only be used to create value types, so we only need to check Newobj.
            // Ref. https://docs.microsoft.com/en-US/dotnet/api/system.reflection.emit.opcodes.newobj?view=netcore-2.1
            // Ref. https://docs.microsoft.com/en-US/dotnet/api/system.reflection.emit.opcodes.initobj?view=netcore-2.1
            if (instruction.OpCode.Code == Code.Newobj)
            {
                if (instruction.Operand is MethodReference methodRef)
                {
                    if (!methodRef.DeclaringType.IsValueType)
                    {
                        return new List<ValidationResult>
                        {
                            new NewObjValidationResult(method)
                        };
                    }
                }
            }

            return Enumerable.Empty<NewObjValidationResult>();
        }

        public class NewObjValidationResult : MethodDefinitionValidationResult
        {
            public NewObjValidationResult(MethodDefinition method) 
                : base(method, "", $"{method.FullName} is invalid [{ErrorType}]")
            {
            }
        }
    }
}