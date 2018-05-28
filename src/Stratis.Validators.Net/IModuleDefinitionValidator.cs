using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ModuleDefinition module);
    }
}