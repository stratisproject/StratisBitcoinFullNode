using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    /// <summary>
    /// Validates whether a type defines any fields
    /// </summary>
    public class FieldDefinitionValidator : ITypeDefinitionValidator
    {
         public IEnumerable<ValidationResult> Validate(TypeDefinition type)
         {
             // Don't apply to nested enums or structs
             if (type.IsNested && type.IsValueType)
                 return Enumerable.Empty<TypeDefinitionValidationResult>();

            var errors = new List<TypeDefinitionValidationResult>();

             foreach (FieldDefinition field in type.Fields)
             {
                 if (field.HasConstant) continue;

                errors.Add(new FieldDefinitionValidationResult(
                     "",
                    "Field usage",
                    $"Non-constant field {field.Name} defined in Type \"{type.Name}\". Fields are not persisted and may change values between calls."
                 ));
             }

             return errors;
         }

        public class FieldDefinitionValidationResult : TypeDefinitionValidationResult
        {
            public FieldDefinitionValidationResult(TypeDefinition type, FieldDefinition field)
                : base("", "Field usage", $"Non-constant field {field.Name} defined in Type \"{type.Name}\". Fields are not persisted and may change values between calls.")
            {
            }

            public FieldDefinitionValidationResult(string subjectName, string validationType, string message) 
                : base(subjectName, validationType, message)
            {
            }
        }
    }
}