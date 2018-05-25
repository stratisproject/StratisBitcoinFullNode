using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(TypeDefinition type);
    }
}