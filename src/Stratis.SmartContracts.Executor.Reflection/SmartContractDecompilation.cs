using Mono.Cecil;

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
    }
}