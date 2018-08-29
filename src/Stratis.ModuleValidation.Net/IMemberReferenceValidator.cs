using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.ModuleValidation.Net
{
    public interface IMemberReferenceValidator
    {
        IEnumerable<ValidationResult> Validate(MemberReference memberReference);
    }
}