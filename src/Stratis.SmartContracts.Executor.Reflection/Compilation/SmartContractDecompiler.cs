using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
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
            var moduleDefinition = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters {AssemblyResolver = resolver});

            return new SmartContractDecompilation(moduleDefinition);
        }
    }
}