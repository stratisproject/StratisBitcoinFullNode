using System.IO;
using Mono.Cecil;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Loader;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public sealed class SmartContractDecompilation
    {
        public TypeDefinition BaseType
        {
            get { return this.ContractType.BaseType.Resolve(); }
        }

        public TypeDefinition ContractType { get; set; }

        public ModuleDefinition ModuleDefinition { get; set; }

        /// <summary>
        /// Rewrites the smart contract constructor of the <see cref="ModuleDefinition"/> to include opcodes for measuring gas consumption.
        /// </summary>
        /// <remarks>
        /// TODO - Make this a generic 'rewrite' method and pass in a rewriter.
        /// </remarks>
        public void InjectConstructorGas()
        {
            this.ModuleDefinition = SmartContractGasInjector.AddGasCalculationToConstructor(this.ModuleDefinition, this.ContractType.Name);
        }

        /// <summary>
        /// Serializes the <see cref="ModuleDefinition"/> to contract bytecode.
        /// </summary>
        public ContractByteCode ToByteCode()
        {
            using (var ms = new MemoryStream())
            {
                this.ModuleDefinition.Write(ms);

                return (ContractByteCode) ms.ToArray();
            }
        }

        /// <summary>
        /// Validates the <see cref="SmartContractDecompilation"/> using the supplied validator.
        /// </summary>
        public SmartContractValidationResult Validate(ISmartContractValidator validator)
        {
            return validator.Validate(this.ModuleDefinition);
        }
    }
}