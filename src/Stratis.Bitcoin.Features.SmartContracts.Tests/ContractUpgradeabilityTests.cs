using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractUpgradeabilityTests
    {
        public class TestAssemblyLoadContext : AssemblyLoadContext
        {
            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }
        }

        [Fact]
        public void Load_Contract_Compiled_Against_V1_With_V2_SmartContracts_Succeeds()
        {
            var source = @"
using Stratis.SmartContracts;

public class TestContract : SmartContract
{
    public TestContract(ISmartContractState state) : base(state) {}

    public void AMethod(){}
}
";
            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(Path.Combine(basePath, "Packages", "netcoreapp2.1", "System.Runtime.dll"))
            };

            var version1DllPath = Path.Combine(basePath, "Packages", "1.0.0-TEST", "Stratis.SmartContracts.dll");

            // Version 2.0.0-TEST adds string TestMethod() to Stratis.SmartContracts.SmartContract
            // and GetString() to Stratis.SmartContracts.ISmartContractState
            var version2DllPath = Path.Combine(basePath, "Packages", "2.0.0-TEST", "Stratis.SmartContracts.dll");
            
            references.Add(MetadataReference.CreateFromFile(version1DllPath));

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            CSharpCompilation compilation = CSharpCompilation.Create(
                "smartContract",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    checkOverflow: true));

            byte[] version1CompiledContract;

            using (var dllStream = new MemoryStream())
            {
                EmitResult emitResult = compilation.Emit(dllStream);
                
                Assert.True(emitResult.Success);

                version1CompiledContract = dllStream.ToArray();
            }

            var alc = new TestAssemblyLoadContext();

            var version2Assembly = alc.LoadFromAssemblyPath(version2DllPath);

            Assert.Equal(Version.Parse("2.0.0.0"), version2Assembly.GetName().Version);

            var version1ContractMemoryStream = new MemoryStream(version1CompiledContract);     
            var version1ContractAssembly = alc.LoadFromStream(version1ContractMemoryStream);
            version1ContractMemoryStream.Dispose();

            var type = version1ContractAssembly.ExportedTypes.First(t => t.Name == "TestContract");

            var method = type.BaseType.GetMethod("TestMethod");
            
            // If this condition is not null, we have a v1 contract referencing a v2 assembly
            Assert.NotNull(method);
        }
    }
}
