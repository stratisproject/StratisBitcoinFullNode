using System;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Validation;

namespace Stratis.SmartContracts.CLR
{
    public interface IContractModuleDefinition : IDisposable
    {
        /// <summary>
        /// Returns the <see cref="TypeDefinition"/> of the contract denoted as the module's entry point with a <see cref="DeployAttribute"/>.
        /// If no entry point is defined, the first <see cref="TypeDefinition"/> will be chosen.
        /// </summary>
        TypeDefinition ContractType { get; }

        /// <summary>
        /// The underlying <see cref="Mono.Cecil.ModuleDefinition"/> representing the contract.
        /// </summary>
        ModuleDefinition ModuleDefinition { get; }

        /// <summary>
        /// Serializes the <see cref="ContractModuleDefinition.ModuleDefinition"/> to contract bytecode.
        /// </summary>
        ContractByteCode ToByteCode();

        /// <summary>
        /// Validates the <see cref="ContractModuleDefinition"/> using the supplied validator.
        /// </summary>
        SmartContractValidationResult Validate(ISmartContractValidator validator);

        /// <summary>
        /// Rewrite the ModuleDefintion using an ILRewriter.
        /// </summary>
        void Rewrite(IILRewriter rewriter);

        /// <summary>
        /// Returns the name of the property getter method for the property with this name on the given type,
        /// or null if no property with this name exists.
        /// </summary>        
        string GetPropertyGetterMethodName(string typeName, string propertyName);
    }
}