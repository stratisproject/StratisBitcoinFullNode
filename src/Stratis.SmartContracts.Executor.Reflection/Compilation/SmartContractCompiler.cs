using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Stratis.SmartContracts.Executor.Reflection.Compilation
{
    /// <summary>
    /// Helpful methods to handle compilation of .cs files.
    /// </summary>
    public static class SmartContractCompiler
    {
        private const string AssemblyName = "smartContract";

        /// <summary>
        /// Get the compiled bytecode for the specified file.
        /// </summary>
        /// <param name="path"></param>
        public static SmartContractCompilationResult CompileFile(string path)
        {
            string source = File.ReadAllText(path);
            return Compile(source);
        }

        /// <summary>
        /// Get the compiled bytecode for the specified C# source code.
        /// </summary>
        /// <param name="source"></param>
        public static SmartContractCompilationResult Compile(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            // @TODO - Use OptimizationLevel.Release once we switch to injecting compiler options
            CSharpCompilation compilation = CSharpCompilation.Create(
                AssemblyName,
                new[] { syntaxTree },
                GetReferences(),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary, 
                    checkOverflow: true));


            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                    return SmartContractCompilationResult.Failed(emitResult.Diagnostics);

                return SmartContractCompilationResult.Succeeded(dllStream.ToArray());
            }
        }

        /// <summary>
        /// Gets all references needed for compilation. Ideally should use the same list as the contract validator.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<MetadataReference> GetReferences()
        {
            return ReferencedAssemblyResolver.AllowedAssemblies.Select(a => MetadataReference.CreateFromFile(a.Location));
        }
    }
}