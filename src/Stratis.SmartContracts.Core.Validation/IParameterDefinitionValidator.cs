using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IParameterDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ParameterDefinition parameter);
    }
}