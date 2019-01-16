using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace Stratis.SmartContracts.CLR.Compilation
{
    /// <summary>
    /// Helpful methods to handle compilation of .cs files.
    /// </summary>
    public static class ContractCompiler
    {
        private const string AssemblyName = "SmartContract";

        /// <summary>
        /// Get the compiled bytecode for the specified file.
        /// </summary>
        /// <param name="path"></param>
        public static ContractCompilationResult CompileFile(string path, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            string source = File.ReadAllText(path);
            return Compile(source, optimizationLevel);
        }

        /// <summary>
        /// Compile all of the files in a directory, with the option of compiling a certain namespace.
        /// </summary>
        public static ContractCompilationResult CompileDirectory(string path, string selectedNamespace = null, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            // Get the syntax tree for every file in the given path.
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            IEnumerable<SyntaxTree> syntaxTrees = files.Select(item => CSharpSyntaxTree.ParseText(File.ReadAllText(item)).WithFilePath(item));

            // If we're not interested in any specific namespace then compile everything in the directory.
            if (selectedNamespace == null)
                return Compile(syntaxTrees, optimizationLevel);

            // From all of these, work out which ones contain code in the given namespace.
            List<SyntaxTree> selectedNamespaceTrees = new List<SyntaxTree>();
            foreach(SyntaxTree tree in syntaxTrees)
            {
                IEnumerable<NamespaceDeclarationSyntax> namespaceDeclarationSyntaxes = tree.GetRoot().DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                if (namespaceDeclarationSyntaxes.Any(x=>x.Name.ToString() == selectedNamespace))
                {
                    selectedNamespaceTrees.Add(tree);
                }
            }

            return Compile(selectedNamespaceTrees, optimizationLevel);
        }

        /// <summary>
        /// Get the compiled bytecode for the specified C# source code.
        /// </summary>
        /// <param name="source"></param>
        public static ContractCompilationResult Compile(string source, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
            return Compile(new[] { syntaxTree }, optimizationLevel);
        }

        private static ContractCompilationResult Compile(IEnumerable<SyntaxTree> syntaxTrees, OptimizationLevel optimizationLevel)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                AssemblyName,
                syntaxTrees,
                GetReferences(),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    checkOverflow: true,
                    optimizationLevel: optimizationLevel,
                    deterministic: true)
                );


            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                    return ContractCompilationResult.Failed(emitResult.Diagnostics);

                return ContractCompilationResult.Succeeded(dllStream.ToArray());
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