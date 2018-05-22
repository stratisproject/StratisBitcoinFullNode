using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates whether a type defines any fields
    /// </summary>
    public class FieldDefinitionValidator : ITypeDefinitionValidator
    {
         public IEnumerable<SmartContractValidationError> Validate(TypeDefinition type)
         {
            var errors = new List<SmartContractValidationError>();

             foreach (FieldDefinition field in type.Fields)
             {
                 if (field.HasConstant) continue;
                  
                 errors.Add(new SmartContractValidationError(
                     "",
                     "",
                     "Field usage",
                     $"Non-constant field {field.Name} defined in Type \"{type.Name}\". Fields are not persisted and may change values between calls."
                 ));
             }

             return errors;
         }
    }

    /// <summary>
    /// Validates that a method does not set any local fields
    /// </summary>
    public class FieldReferenceValidator : IMethodDefinitionValidator
    {
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

            if (instruction.Operand is FieldDefinition fieldDefinition)
            {
                errors.Add(new SmartContractValidationError(
                    method.Name,
                    method.FullName,
                    "Field usage",
                    $"Field {fieldDefinition.Name} defined or used locally. This can be dangerous."
                ));
            }

            return errors;
        }
    }
}