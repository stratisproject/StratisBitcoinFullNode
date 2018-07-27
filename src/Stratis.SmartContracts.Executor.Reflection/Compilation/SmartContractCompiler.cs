using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        /// Compile all of the files in a directory, with the option of compiling a certain namespace.
        /// </summary>
        public static SmartContractCompilationResult CompileDirectory(string path, string selectedNamespace = null)
        {
            // Get the syntax tree for every file in the given path.
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            IEnumerable<SyntaxTree> syntaxTrees = files.Select(item => CSharpSyntaxTree.ParseText(File.ReadAllText(item)).WithFilePath(item));

            // If we're not interested in any specific namespace then compile everything in the directory.
            if (selectedNamespace == null)
                return Compile(syntaxTrees);

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

            return Compile(selectedNamespaceTrees);
        }

        /// <summary>
        /// Get the compiled bytecode for the specified C# source code.
        /// </summary>
        /// <param name="source"></param>
        public static SmartContractCompilationResult Compile(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
            return Compile(new[] { syntaxTree });
        }

        private static SmartContractCompilationResult Compile(IEnumerable<SyntaxTree> syntaxTrees)
        {
            // @TODO - Use OptimizationLevel.Release once we switch to injecting compiler options
            CSharpCompilation compilation = CSharpCompilation.Create(
                AssemblyName,
                syntaxTrees,
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