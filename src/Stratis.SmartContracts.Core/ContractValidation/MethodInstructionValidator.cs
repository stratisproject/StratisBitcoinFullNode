﻿using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not contain any invalid opcodes or field references
    /// </summary>
    public class MethodInstructionValidator : IMethodDefinitionValidator
    {
        /// <summary>
        /// There may be other intrinsics (values that are inserted via the compiler and are different per machine).
        /// </summary>
        private static readonly HashSet<string> RedLightFields = new HashSet<string>
        {
            "System.Boolean System.BitConverter::IsLittleEndian" // Can we get this from reflection rather than hard code?
        };

        /// <summary>
        /// Any float-based instructions. Not allowed.
        /// </summary>
        private static readonly HashSet<OpCode> RedLightOpCodes = new HashSet<OpCode>
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

        public IEnumerable<FormatValidationError> Validate(MethodDefinition method)
        {
            if (method.Body?.Instructions == null)
                return Enumerable.Empty<FormatValidationError>();

            var errors = new List<FormatValidationError>();

            foreach (Instruction instruction in method.Body.Instructions)
            {
                IEnumerable<FormatValidationError> instructionValidationResult = ValidateInstruction(method, instruction);
                errors.AddRange(instructionValidationResult);
            }

            return errors;
        }

        private static IEnumerable<FormatValidationError> ValidateInstruction(MethodDefinition method, Instruction instruction)
        {
            var errors = new List<FormatValidationError>();

            if (RedLightOpCodes.Contains(instruction.OpCode))
            {
                errors.Add(new FormatValidationError(
                    method.Name,
                    method.FullName,
                    "Float usage",
                    $"Float or double used."
                ));
            }

            if (instruction.Operand is FieldReference fieldReference)
            {
                if (RedLightFields.Contains(fieldReference.FullName))
                {
                    errors.Add(new FormatValidationError(
                        method.Name,
                        method.FullName,
                        $"Use of {fieldReference.FullName}",
                        $"{fieldReference.FullName} is not deterministic."
                    ));
                }
            }

            return errors;
        }
    }
}