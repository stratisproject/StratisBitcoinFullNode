using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation.Validators.Type
{
    /// <summary>
    /// Checks that a typedef is nested before validating
    /// </summary>
    public class NestedValidator : ITypeDefinitionValidator
    {
        private readonly ITypeDefinitionValidator validator;

        public NestedValidator(ITypeDefinitionValidator validator)
        {
            this.validator = validator;
        }

        public IEnumerable<ValidationResult> Validate(TypeDefinition typeDef)
        {
            if (typeDef.IsNested)
            {
                return this.validator.Validate(typeDef);
            }

            return Enumerable.Empty<ValidationResult>();
        }
    }
}