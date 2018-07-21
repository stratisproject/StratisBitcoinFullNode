using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface ITypeDefValidator
    {
        bool Validate(TypeDefinition type);
        TypeDefinitionValidationResult CreateError(TypeDefinition type);
    }

    public interface ITypeDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(TypeDefinition type);
    }
}