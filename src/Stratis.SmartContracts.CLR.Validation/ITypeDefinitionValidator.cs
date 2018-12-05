using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(TypeDefinition type);
    }
}