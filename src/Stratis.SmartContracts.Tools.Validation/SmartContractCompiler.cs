using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stratis.SmartContracts.Tools.Validation
{
    /// <summary>
    /// Compiler for a C# SmartContract.
    /// </summary>
    public class SmartContractCompiler
    {
        private readonly string assemblyName;

        public SmartContractCompiler(string assemblyName = "smartContract")
        {
            this.assemblyName = assemblyName;
        }

        /// <summary>
        /// Compiles the provided source code string into a byte array.
        /// </summary>
        /// <param name="source"></param>
        /// <returns>The compiled contract bytecode, or any errors</returns>
        public SmartContractCompilationResult Compile(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                this.assemblyName,
                new[] { syntaxTree },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var dllStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(dllStream, pdbStream);

                if (!emitResult.Success)
                {
                    return SmartContractCompilationResult.Failed(emitResult.Diagnostics);
                }                    

                return SmartContractCompilationResult.Succeeded(dllStream.ToArray());
            }
        }

        /// <summary>
        /// Gets all references needed for compilation. Ideally should use the same list as the contract validator.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<MetadataReference> GetReferences()
        {
            // TODO move these to a references class
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

            var referencePaths = new HashSet<string>
            {
                Path.Combine(assemblyPath, "System.Private.CoreLib.dll"),
                Path.Combine(assemblyPath, "System.Runtime.dll"),
                typeof(Address).Assembly.Location,
                typeof(SmartContract).Assembly.Location,
                typeof(Enumerable).Assembly.Location
            };

            return referencePaths.Select(p => MetadataReference.CreateFromFile(p));
        }
    }
}
