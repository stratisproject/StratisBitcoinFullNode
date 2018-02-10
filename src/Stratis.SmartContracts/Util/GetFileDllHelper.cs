using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace Stratis.SmartContracts.Util
{
    internal static class GetFileDllHelper
    {
        public static byte[] GetAssemblyBytesFromFile(string filename)
        {
            string source = File.ReadAllText(filename);
            return GetAssemblyBytesFromSource(source);
        }

        public static byte[] GetAssemblyBytesFromSource(string source)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                "smartContract",
                new[] { syntaxTree },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var dllStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(dllStream, pdbStream);
                if (!emitResult.Success)
                    throw new Exception("Compilation didn't work yo!");

                return dllStream.ToArray();
            }
        }

        // So heinous but gets all references needed for compilation. 
        private static IList<MetadataReference> GetReferences()
        {
            var dd = typeof(Enumerable).Assembly.Location;
            DirectoryInfo coreDir = Directory.GetParent(dd);

            List<MetadataReference> references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "mscorlib.dll"),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            };

            AssemblyName[] referencedAssemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            foreach (AssemblyName referencedAssembly in referencedAssemblies)
            {
                var loadedAssembly = Assembly.Load(referencedAssembly);

                references.Add(MetadataReference.CreateFromFile(loadedAssembly.Location));
            }
            references.Add(MetadataReference.CreateFromFile(typeof(Address).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(CompiledSmartContract).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Dictionary<object, object>).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(HttpClient).Assembly.Location));
            return references;
        }
    }
}