using System.Collections.Generic;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<FormatValidationError> Validate(ModuleDefinition module);
    }
}