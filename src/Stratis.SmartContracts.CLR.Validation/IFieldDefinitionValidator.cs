using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public interface IFieldDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(FieldDefinition field);
    }
}