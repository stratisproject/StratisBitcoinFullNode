
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Stratis.SmartContracts.ContractValidation
{
    public interface ISmartContractValidator
    {
        SmartContractValidationResult Validate(SmartContractDecompilation decompilation);
    }

    public interface IMethodDefinitionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(MethodDefinition method);
    }

    public interface IInstructionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(Instruction instruction);
    }
}
