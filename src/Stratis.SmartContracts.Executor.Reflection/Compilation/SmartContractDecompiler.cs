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
        /// <remarks>TODO: Fix using in MemoryStream.</remarks>
        public static IContractModuleDefinition GetModuleDefinition(byte[] bytes, IAssemblyResolver assemblyResolver = null)
        {
            IAssemblyResolver resolver = assemblyResolver ?? new DefaultAssemblyResolver();
            var moduleDefinition = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters {AssemblyResolver = resolver});

            return new ContractModuleDefinition(moduleDefinition);
        }
    }
}