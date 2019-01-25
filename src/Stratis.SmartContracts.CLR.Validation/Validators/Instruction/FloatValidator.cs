using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Instruction
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not contain any float opcodes
    /// </summary>
    public class FloatValidator : IInstructionValidator
    {
        public const string FloatValidationType = "Float usage";
        public const string FloatValidationMessage = "Float or double used.";

        /// <summary>
        /// Any float-based instructions. Not allowed.
        /// </summary>
        private static readonly HashSet<OpCode> FloatOpCodes = new HashSet<OpCode>
        {
            OpCodes.Ldc_R4,
            OpCodes.Ldc_R8,
            OpCodes.Ldelem_R4,
            OpCodes.Ldelem_R8,
            OpCodes.Conv_R_Un,
            OpCodes.Conv_R4,
            OpCodes.Conv_R8,
            OpCodes.Ldind_R4,
            OpCodes.Ldind_R8,
            OpCodes.Stelem_R4,
            OpCodes.Stelem_R8,
            OpCodes.Stind_R4,
            OpCodes.Stind_R8
        };

        public IEnumerable<ValidationResult> Validate(Mono.Cecil.Cil.Instruction instruction, MethodDefinition method)
        {
            var errors = new List<FloatValidationResult>();

            if (IsFloat(instruction))
            {
                errors.Add(new FloatValidationResult(method));
            }

            return errors;
        }

        public static bool IsFloat(Mono.Cecil.Cil.Instruction instruction)
        {
            return FloatOpCodes.Contains(instruction.OpCode);
        }

        public class FloatValidationResult : MethodDefinitionValidationResult
        {
            public FloatValidationResult(MethodDefinition method) 
                : base(method.FullName,
                    FloatValidationType,
                    FloatValidationMessage)
            {
            }
        }
    }
}