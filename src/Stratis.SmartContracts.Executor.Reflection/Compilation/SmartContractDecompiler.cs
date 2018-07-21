using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Stratis.ModuleValidation.Net;
using Stratis.SmartContracts.Core.Validation;

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
            var result = new SmartContractDecompilation
            {
                ModuleDefinition = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver })
            };

            IEnumerable<TypeDefinition> developedTypes = result.ModuleDefinition.GetDevelopedTypes();

            if (developedTypes.Count() == 1)
            {
                // If there is only one type, take that.
                result.ContractType = developedTypes.First();
            }
            else
            {
                // Otherwise, we need the type with DeployAttribute.
                result.ContractType = developedTypes.FirstOrDefault(x => x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(DeployAttribute).Name));
            }

            return result;
        }
    }
}