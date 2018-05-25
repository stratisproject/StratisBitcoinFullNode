using System.Collections.Generic;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<FormatValidationError> Validate(TypeDefinition type);
    }
}