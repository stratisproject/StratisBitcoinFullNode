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

            List<TypeDefinition> developedTypes = moduleDefinition.GetDevelopedTypes().ToList();

            TypeDefinition contractType;
            if (developedTypes.Count() == 1)
            {
                // If there is only one type, take that.
                contractType = developedTypes.First();
            }
            else
            {
                // Otherwise, we need the type with DeployAttribute.
                contractType = developedTypes.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name));
            }

            // If contract type wasn't set yet, set the first.
            contractType = contractType ?? developedTypes.FirstOrDefault();

            return new SmartContractDecompilation(moduleDefinition, contractType);
        }
    }
}