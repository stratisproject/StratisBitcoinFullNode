using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface IModuleDefinitionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(ModuleDefinition module);
    }
}