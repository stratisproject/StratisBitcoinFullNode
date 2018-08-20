using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public interface IContractModuleDefinitionReader
    {
        /// <summary>
        /// Reads a <see cref="IContractModuleDefinition"/> from the given byte code.
        /// </summary>
        IContractModuleDefinition Read(byte[] bytes, IAssemblyResolver assemblyResolver = null);
    }
}