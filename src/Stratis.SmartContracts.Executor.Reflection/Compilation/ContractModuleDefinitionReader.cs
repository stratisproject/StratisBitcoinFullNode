using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public class ContractModuleDefinitionReader : IContractModuleDefinitionReader
    {
        /// <inheritdoc />
        public Result<IContractModuleDefinition> Read(byte[] bytes)
        {
            return ContractDecompiler.GetModuleDefinition(bytes, null);
        }
    }
}