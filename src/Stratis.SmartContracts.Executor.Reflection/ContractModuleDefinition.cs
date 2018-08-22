using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Loader;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents a low-level contract module that can be modified (via IL rewriting) and validated.
    /// </summary>
    public sealed class ContractModuleDefinition : IContractModuleDefinition
    {
        private List<TypeDefinition> developedTypes;

        private TypeDefinition contractType;
        private readonly MemoryStream stream;

        public ContractModuleDefinition(ModuleDefinition moduleDefinition, MemoryStream stream)
        {
            this.ModuleDefinition = moduleDefinition;
            this.stream = stream;
        }

        /// <inheritdoc />
        public List<TypeDefinition> DevelopedTypes => this.developedTypes ?? (this.developedTypes = this.ModuleDefinition.GetDevelopedTypes().ToList());

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public ContractByteCode ToByteCode()
        {
            using (var ms = new MemoryStream())
            {
                this.ModuleDefinition.Write(ms);

                return (ContractByteCode) ms.ToArray();
            }
        }

        /// <inheritdoc />
        public SmartContractValidationResult Validate(ISmartContractValidator validator)
        {
            return validator.Validate(this.ModuleDefinition);
        }

        /// <summary>
        /// Rewrites the <see cref="ModuleDefinition"/> on the type with this name to include opcodes for measuring gas consumption.
        /// </summary>
        /// <remarks>
        /// TODO - Make this a generic 'rewrite' method and pass in a rewriter.
        /// </remarks>
        public void InjectMethodGas(string typeName, string methodName)
        {
            this.ModuleDefinition = SmartContractGasInjector.AddGasCalculationToContractMethod(this.ModuleDefinition, typeName, methodName);
        }

        public void Dispose()
        {
            this.stream?.Dispose();
            this.ModuleDefinition?.Dispose();
        }
    }
}