using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;

namespace Stratis.SmartContracts.CLR
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

        /// <inheritdoc />
        public void Rewrite(IILRewriter rewriter)
        {
            this.ModuleDefinition = rewriter.Rewrite(this.ModuleDefinition);
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

        public string GetPropertyGetterMethodName(string typeName, string propertyName)
        {
            return this.DevelopedTypes
                       .FirstOrDefault(t => t.Name == typeName)
                       ?.Properties
                       .FirstOrDefault(p => p.Name == propertyName)
                       ?.GetMethod.Name;
        }

        public void Dispose()
        {
            this.stream?.Dispose();
            this.ModuleDefinition?.Dispose();
        }
    }
}