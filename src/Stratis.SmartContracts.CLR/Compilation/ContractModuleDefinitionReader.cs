using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR.Compilation
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