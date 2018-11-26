using System.Collections.Generic;
using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IMemberReferenceValidator
    {
        IEnumerable<ValidationResult> Validate(MemberReference memberReference);
    }
}