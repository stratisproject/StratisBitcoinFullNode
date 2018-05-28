using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates whether a type defines any fields
    /// </summary>
    public class FieldDefinitionValidator : ITypeDefinitionValidator
    {
         public IEnumerable<ValidationResult> Validate(TypeDefinition type)
         {
            var errors = new List<ValidationResult>();

             foreach (FieldDefinition field in type.Fields)
             {
                 if (field.HasConstant) continue;
                  
                 errors.Add(new ValidationResult(
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
        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (method.Body?.Instructions == null)
                return Enumerable.Empty<ValidationResult>();

            var errors = new List<ValidationResult>();

            foreach (Instruction instruction in method.Body.Instructions)
            {
                IEnumerable<ValidationResult> instructionValidationResult = ValidateInstruction(method, instruction);
                errors.AddRange(instructionValidationResult);
            }

            return errors;
        }

        private static IEnumerable<ValidationResult> ValidateInstruction(MethodDefinition method, Instruction instruction)
        {
            var errors = new List<ValidationResult>();

            if (instruction.Operand is FieldDefinition fieldDefinition)
            {
                errors.Add(new ValidationResult(
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