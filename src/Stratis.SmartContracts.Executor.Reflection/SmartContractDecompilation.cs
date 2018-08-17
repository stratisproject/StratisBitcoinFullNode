using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Loader;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public sealed class SmartContractDecompilation
    {
        private List<TypeDefinition> developedTypes;

        private TypeDefinition contractType;

        public SmartContractDecompilation(ModuleDefinition moduleDefinition)
        {
            this.ModuleDefinition = moduleDefinition;
        }

        public TypeDefinition BaseType
        {
            get { return this.ContractType.BaseType.Resolve(); }
        }

        public List<TypeDefinition> DevelopedTypes => this.developedTypes ?? (this.developedTypes = this.ModuleDefinition.GetDevelopedTypes().ToList());

        public TypeDefinition ContractType
        {
            get
            {
                if (this.contractType == null)
                {
                    this.contractType = this.DevelopedTypes.Count == 1
                        ? this.DevelopedTypes.First()
                        : this.DevelopedTypes.FirstOrDefault(x =>
                            x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name));

                    this.contractType = this.contractType ?? this.DevelopedTypes.FirstOrDefault();
                }

                return this.contractType;
            }
        }

        public ModuleDefinition ModuleDefinition { get; private set; }

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