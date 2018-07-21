using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// Validates a TypeDefinition's nested Types using the supplied validator
    /// </summary>
    public class NestedTypeValidator : ITypeDefinitionValidator
    {
        private readonly ITypeDefinitionValidator validator;

        public NestedTypeValidator(ITypeDefinitionValidator validator)
        {
            this.validator = validator;
        }

        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            return type.NestedTypes.SelectMany(t => this.validator.Validate(t));
        }
    }
}