using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public class ContractModuleDefinitionReader : IContractModuleDefinitionReader
    {
        /// <inheritdoc />
        public IContractModuleDefinition Read(byte[] bytes, IAssemblyResolver assemblyResolver = null)
        {
            return SmartContractDecompiler.GetModuleDefinition(bytes, assemblyResolver);
        }
    }
}