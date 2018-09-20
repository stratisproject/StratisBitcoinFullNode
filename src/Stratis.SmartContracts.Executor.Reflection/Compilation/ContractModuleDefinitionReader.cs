namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public class ContractModuleDefinitionReader : IContractModuleDefinitionReader
    {
        /// <inheritdoc />
        public IContractModuleDefinition Read(byte[] bytes)
        {
            return ContractDecompiler.GetModuleDefinition(bytes, null);
        }
    }
}