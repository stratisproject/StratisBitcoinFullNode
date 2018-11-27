using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ModuleDefinition module);
    }
}