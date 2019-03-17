using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.ILRewrite;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Validation;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Represents a low-level contract module that can be modified (via IL rewriting) and validated.
    /// </summary>
    public sealed class ContractModuleDefinition : IContractModuleDefinition
    {
        private List<TypeDefinition> contractTypes;

        private TypeDefinition contractType;
        private readonly MemoryStream stream;

        public ContractModuleDefinition(ModuleDefinition moduleDefinition, MemoryStream stream)
        {
            this.ModuleDefinition = moduleDefinition;
            this.stream = stream;
        }

        /// <inheritdoc />
        public List<TypeDefinition> ContractTypes => this.contractTypes ?? (this.contractTypes = this.ModuleDefinition.GetContractTypes().ToList());

        /// <inheritdoc />
        public TypeDefinition ContractType
        {
            get
            {
                if (this.contractType == null)
                {
                    this.contractType = this.ContractTypes.Count == 1
                        ? this.ContractTypes.First()
                        : this.ContractTypes.FirstOrDefault(x =>
                            x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name));
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
            try
            {
                return validator.Validate(this.ModuleDefinition);
            }
            catch (Exception e)
            {
                return new SmartContractValidationResult(new[]
                {
                    new ModuleDefinitionValidationResult("Error validating module: " + e.Message)
                });
            }
        }

        public string GetPropertyGetterMethodName(string typeName, string propertyName)
        {
            return this.ContractTypes
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