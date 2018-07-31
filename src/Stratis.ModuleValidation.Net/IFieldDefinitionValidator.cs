using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IFieldDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(FieldDefinition field);
    }
}