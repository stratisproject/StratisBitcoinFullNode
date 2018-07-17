using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IMethodDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(MethodDefinition method);
    }
}