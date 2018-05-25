using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<FormatValidationError> Validate(ModuleDefinition module);
    }
}