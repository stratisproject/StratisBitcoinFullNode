using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public interface IParameterDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ParameterDefinition parameter);
    }
}