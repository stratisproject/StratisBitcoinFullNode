using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(TypeDefinition type);
    }
}