using Mono.Cecil;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractDecompilation
    {
        public ModuleDefinition ModuleDefinition { get; set; }

        public TypeDefinition ContractType { get; set; }

        public TypeDefinition BaseType => this.ContractType.BaseType.Resolve();
    }
}