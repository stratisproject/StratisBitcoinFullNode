using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR.Compilation
{
    public interface IContractModuleDefinitionReader
    {
        /// <summary>
        /// Reads a <see cref="IContractModuleDefinition"/> from the given byte code.
        /// </summary>
        Result<IContractModuleDefinition> Read(byte[] bytes);
    }
}