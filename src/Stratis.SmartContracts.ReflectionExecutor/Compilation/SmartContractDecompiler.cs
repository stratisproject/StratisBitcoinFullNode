using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Stratis.SmartContracts.ReflectionExecutor.Compilation
{
    public static class SmartContractDecompiler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>TODO: Ensure that AppContext.BaseDirectory is robust here.</remarks>
        /// <remarks>TODO: Fix using in MemoryStream.</remarks>
        public static SmartContractDecompilation GetModuleDefinition(byte[] bytes, IAssemblyResolver assemblyResolver = null)
        {
            IAssemblyResolver resolver = assemblyResolver ?? new DefaultAssemblyResolver();
            var result = new SmartContractDecompilation
            {
                ModuleDefinition = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver })
            };

            result.ContractType = result.ModuleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");

            return result;
        }
    }
}