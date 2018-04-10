using Mono.Cecil;

namespace Stratis.SmartContracts.Core.Compilation
{
    public sealed class SmartContractDecompilation
    {
        public TypeDefinition BaseType
        {
            get { return this.ContractType.BaseType.Resolve(); }
        }

        public TypeDefinition ContractType { get; set; }

        public ModuleDefinition ModuleDefinition { get; set; }
    }
}