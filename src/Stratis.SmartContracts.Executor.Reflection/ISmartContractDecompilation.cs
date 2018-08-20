using System.Collections.Generic;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Loader;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractDecompilation
    {
        /// <summary>
        /// The <see cref="TypeDefinition"/>s contained in the module, excluding those that are compiler or framework generated.
        /// </summary>
        List<TypeDefinition> DevelopedTypes { get; }

        /// <summary>
        /// Returns the <see cref="TypeDefinition"/> of the contract denoted as the module's entry point with a <see cref="DeployAttribute"/>.
        /// If no entry point is defined, the first <see cref="TypeDefinition"/> will be chosen.
        /// </summary>
        TypeDefinition ContractType { get; }

        /// <summary>
        /// The underlying <see cref="Mono.Cecil.ModuleDefinition"/> representing the contract.
        /// </summary>
        ModuleDefinition ModuleDefinition { get; }

        void InjectConstructorGas();

        /// <summary>
        /// Serializes the <see cref="SmartContractDecompilation.ModuleDefinition"/> to contract bytecode.
        /// </summary>
        ContractByteCode ToByteCode();

        /// <summary>
        /// Validates the <see cref="SmartContractDecompilation"/> using the supplied validator.
        /// </summary>
        SmartContractValidationResult Validate(ISmartContractValidator validator);

        void InjectMethodGas(string typeName, string methodName);
    }
}