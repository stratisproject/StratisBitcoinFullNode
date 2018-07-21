using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net.Determinism
{
    /// <summary>
    /// Validates that a <see cref="Mono.Cecil.MethodDefinition"/> does not contain any fields defined in an external Type
    /// </summary>
    public class WhitelistedFieldValidator : IFieldDefinitionValidator
    {
        private readonly IEnumerable<string> typeWhitelist;

        public WhitelistedFieldValidator(IEnumerable<string> typeWhitelist)
        {
            this.typeWhitelist = typeWhitelist;
        }

        public IEnumerable<ValidationResult> Validate(FieldDefinition field)
        {
            if (!this.typeWhitelist.Contains(field.DeclaringType.FullName))
            {
                return new[] {new WhitelistedFieldValidationResult(field)};
            }

            return Enumerable.Empty<ValidationResult>();
        }

        public class WhitelistedFieldValidationResult : ValidationResult
        {
            public WhitelistedFieldValidationResult(FieldDefinition field) 
                : base(field.FullName, "Forbidden field usage", $"Field {field.FullName} cannot be used")
            {
            }
        }
    }
}