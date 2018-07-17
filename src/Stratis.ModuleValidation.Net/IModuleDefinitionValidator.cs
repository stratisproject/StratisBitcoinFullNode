using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ModuleDefinition module);
    }
}