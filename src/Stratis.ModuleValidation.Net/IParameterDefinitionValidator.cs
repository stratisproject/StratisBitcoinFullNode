using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IParameterDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(ParameterDefinition parameter);
    }
}