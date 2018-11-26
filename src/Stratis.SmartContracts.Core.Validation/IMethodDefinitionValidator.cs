using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IMethodDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(MethodDefinition method);
    }
}