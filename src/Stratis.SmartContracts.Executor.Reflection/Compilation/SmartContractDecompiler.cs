using System.IO;
using Mono.Cecil;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    public static class SmartContractDecompiler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>TODO: Ensure that AppContext.BaseDirectory is robust here.</remarks>
        public static IContractModuleDefinition GetModuleDefinition(byte[] bytes, IAssemblyResolver assemblyResolver = null)
        {
            IAssemblyResolver resolver = assemblyResolver ?? new DefaultAssemblyResolver();
            var stream = new MemoryStream(bytes);
            var moduleDefinition = ModuleDefinition.ReadModule(stream, new ReaderParameters { AssemblyResolver = resolver });

            return new ContractModuleDefinition(moduleDefinition, stream);
        }
    }
}