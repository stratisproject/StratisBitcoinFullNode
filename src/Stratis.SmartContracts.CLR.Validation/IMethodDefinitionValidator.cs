using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Validation
{
    public interface IMethodDefinitionValidator
    {
        IEnumerable<ValidationResult> Validate(MethodDefinition method);
    }
}