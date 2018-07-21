using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public class CombiningTypeDefinitionValidator : ITypeDefinitionValidator
    {
        private readonly IEnumerable<ITypeDefinitionValidator> validators;

        public CombiningTypeDefinitionValidator(IEnumerable<ITypeDefinitionValidator> validators)
        {
            this.validators = validators;
        }

        public IEnumerable<ValidationResult> Validate(TypeDefinition type)
        {
            return this.validators.SelectMany(v => v.Validate(type));
        }
    }
}