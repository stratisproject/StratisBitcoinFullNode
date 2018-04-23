using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.Core.Compilation;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    public interface ISmartContractValidator
    {
        /// <summary>
        /// Validate all user defined methods in the contract.
        /// <para>
        /// All methods with an empty body will be ignored.
        /// </para>
        /// </summary>
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