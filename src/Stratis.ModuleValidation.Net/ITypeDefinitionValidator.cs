using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(TypeDefinition type);
    }
}