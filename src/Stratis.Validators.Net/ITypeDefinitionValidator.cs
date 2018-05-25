using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public interface ITypeDefinitionValidator
    {
        IEnumerable<FormatValidationError> Validate(TypeDefinition type);
    }
}