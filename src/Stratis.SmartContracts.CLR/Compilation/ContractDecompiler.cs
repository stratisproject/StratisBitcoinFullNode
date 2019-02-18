using System;
using System.IO;
using CSharpFunctionalExtensions;
using Mono.Cecil;

namespace Stratis.SmartContracts.CLR.Compilation
{
    public static class ContractDecompiler
    {
        /// <summary>
        /// Decompile a contract into a Mono.Cecil module.
        /// </summary>
        /// <remarks>TODO: Ensure that AppContext.BaseDirectory is robust here.</remarks>
        public static Result<IContractModuleDefinition> GetModuleDefinition(byte[] bytes, IAssemblyResolver assemblyResolver = null)
        {
            IAssemblyResolver resolver = assemblyResolver ?? new DefaultAssemblyResolver();
            var stream = new MemoryStream(bytes);
            try
            {
                var moduleDefinition = ModuleDefinition.ReadModule(stream, new ReaderParameters { AssemblyResolver = resolver});
                return Result.Ok<IContractModuleDefinition>(new ContractModuleDefinition(moduleDefinition, stream));
            }
            catch (Exception e) // Ideally we would only catch BadImageFormatException but unfortunately Cecil can be unreliable.
            {
                return Result.Fail<IContractModuleDefinition>("Invalid bytecode:" + e.Message);
            }
        }
    }
}