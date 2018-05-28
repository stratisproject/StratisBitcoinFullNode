using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Stratis.SmartContracts.ReflectionExecutor.Compilation;

namespace Stratis.SmartContracts.ReflectionExecutor.ContractValidation
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

    public interface ITypeDefinitionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(TypeDefinition type);
    }

    public interface IModuleDefinitionValidator
    {
        IEnumerable<SmartContractValidationError> Validate(ModuleDefinition module);
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