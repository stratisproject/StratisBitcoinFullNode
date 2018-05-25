using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.Validators.Net
{
    public interface IMethodDefinitionValidator
    {
        IEnumerable<FormatValidationError> Validate(MethodDefinition method);
    }
}