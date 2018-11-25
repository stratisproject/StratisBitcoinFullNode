using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.Core.Validation
{
    public interface IInstructionValidator
    {
        IEnumerable<ValidationResult> Validate(Instruction instruction, MethodDefinition method);
    }
}