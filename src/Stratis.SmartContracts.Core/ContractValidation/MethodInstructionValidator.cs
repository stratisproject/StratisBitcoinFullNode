using System.Collections.Generic;
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
            "System.Boolean System.BitConverter::IsLittleEndian"
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

        public IEnumerable<SmartContractValidationError> Validate(MethodDefinition method)
        {
            if (method.Body?.Instructions == null)
                return Enumerable.Empty<SmartContractValidationError>();

            var errors = new List<SmartContractValidationError>();

            foreach (Instruction instruction in method.Body.Instructions)
            {
                IEnumerable<SmartContractValidationError> instructionValidationResult = ValidateInstruction(method, instruction);
                errors.AddRange(instructionValidationResult);
            }

            return errors;
        }

        private static IEnumerable<SmartContractValidationError> ValidateInstruction(MethodDefinition method, Instruction instruction)
        {
            var errors = new List<SmartContractValidationError>();

            if (RedLightOpCodes.Contains(instruction.OpCode))
            {
                errors.Add(new SmartContractValidationError(
                    method.Name,
                    method.FullName,
                    "Float usage",
                    $"Float used within {method.FullName}"
                ));
            }

            if (instruction.Operand is FieldReference fieldReference)
            {
                if (RedLightFields.Contains(fieldReference.FullName))
                {
                    errors.Add(new SmartContractValidationError(
                        method.Name,
                        method.FullName,
                        $"Use of {fieldReference.FullName}",
                        $"{fieldReference.FullName} in {method.FullName} is not deterministic."
                    ));
                }
            }

            return errors;
        }
    }
}