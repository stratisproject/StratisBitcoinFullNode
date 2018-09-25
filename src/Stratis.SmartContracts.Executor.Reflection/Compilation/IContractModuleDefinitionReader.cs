using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public interface IContractModuleDefinitionReader
    {
        /// <summary>
        /// Reads a <see cref="IContractModuleDefinition"/> from the given byte code.
        /// </summary>
        Result<IContractModuleDefinition> Read(byte[] bytes);
    }
}