using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    /// <summary>
    /// A top-level validator for applying a group of validators to a module definition
    /// </summary>
    public class ModuleValidator
    {
        private readonly IEnumerable<IModuleDefinitionValidator> validators;

        public ModuleValidator(IEnumerable<IModuleDefinitionValidator> validators)
        {
            this.validators = validators;
        }

        public IEnumerable<ValidationResult> Validate(ModuleDefinition moduleDefinition)
        {
            return this.validators.SelectMany(v => v.Validate(moduleDefinition));
        }
    }
}