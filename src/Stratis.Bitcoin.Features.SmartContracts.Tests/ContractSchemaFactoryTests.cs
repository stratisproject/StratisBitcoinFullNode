using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.SmartContracts.CLR.Compilation;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractSchemaFactoryTests
    {
        private const string Code = @"
using Stratis.SmartContracts;

[Deploy]
public class PrimitiveParams : SmartContract
{
    public PrimitiveParams(ISmartContractState state): base(state) {}
    public void AcceptsBool(bool b) {}
    public void AcceptsByte(byte bb) {}
    public void AcceptsByteArray(byte[] ba) {}
    public void AcceptsChar(char c) {}
    public void AcceptsString(string s) {}
    public void AcceptsUint(uint ui) {}
    public void AcceptsUlong(ulong ul) {} 
    public void AcceptsInt(int i) {}
    public void AcceptsLong(long l) {} 
    public void AcceptsAddress(Address a) {}

    public bool SomeProperty {get; set;}

    public void AcceptsAllParams(bool b, byte bb, byte[] ba, char c, string s, uint ui, ulong ul, int i, long l, Address a) {}
}

public class DontDeploy : SmartContract
{
    public DontDeploy(ISmartContractState state): base(state) {}

    public void SomeMethod(string i) {}
}
";
        [Fact]
        public void Map_Parameter_Type_Success()
        {
            var compilationResult = ContractCompiler.Compile(Code);

            var assembly = Assembly.Load(compilationResult.Compilation);

            var mapper = new ContractSchemaFactory();

            MethodInfo methodInfo = assembly.ExportedTypes.First(t => t.Name == "PrimitiveParams").GetMethod("AcceptsAllParams");
            var schema = mapper.Map(methodInfo);
            var properties = schema.Properties;

            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(bool)]().Type, properties["b"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(byte)]().Type, properties["bb"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(byte[])]().Type, properties["ba"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(char)]().Type, properties["c"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(string)]().Type, properties["s"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(uint)]().Type, properties["ui"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(ulong)]().Type, properties["ul"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(int)]().Type, properties["i"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(long)]().Type, properties["l"].Type);
            Assert.Equal(ContractSchemaFactory.PrimitiveTypeMap[typeof(string)]().Type, properties["a"].Type);
        }

        [Fact]
        public void Map_Type_Success()
        {
            var compilationResult = ContractCompiler.Compile(Code);

            var assembly = Assembly.Load(compilationResult.Compilation);

            var mapper = new ContractSchemaFactory();

            Type type = assembly.ExportedTypes.First(t => t.Name == "PrimitiveParams");

            // Maps the methods in a type to schemas.
            IDictionary<string, Schema> mapped = mapper.Map(type);
            
            Assert.Equal("AcceptsBool", mapped["AcceptsBool"].Title);
            Assert.Equal("AcceptsByte", mapped["AcceptsByte"].Title);
            Assert.Equal("AcceptsByteArray", mapped["AcceptsByteArray"].Title);
            Assert.Equal("AcceptsChar", mapped["AcceptsChar"].Title);
            Assert.Equal("AcceptsString", mapped["AcceptsString"].Title);
            Assert.Equal("AcceptsUint", mapped["AcceptsUint"].Title);
            Assert.Equal("AcceptsUlong", mapped["AcceptsUlong"].Title);
            Assert.Equal("AcceptsInt", mapped["AcceptsInt"].Title);
            Assert.Equal("AcceptsLong", mapped["AcceptsLong"].Title);
            Assert.Equal("AcceptsAddress", mapped["AcceptsAddress"].Title);

            Assert.Equal(11, mapped.Count);
        }

        [Fact]
        public void Only_Map_Deployed_Type_Success()
        {
            var compilationResult = ContractCompiler.Compile(Code);

            var assembly = Assembly.Load(compilationResult.Compilation);

            var mapper = new ContractSchemaFactory();

            IDictionary<string, Schema> mapped = mapper.Map(assembly);
            
            Assert.Equal(11, mapped.Count);
            Assert.False(mapped.ContainsKey("SomeMethod"));
        }

        [Fact]
        public void Only_Map_Deployed_Type_Single_Contract_Success()
        {
            string code = @"
using Stratis.SmartContracts;

public class PrimitiveParams : SmartContract
{
    public PrimitiveParams(ISmartContractState state): base(state) {}
    public void SomeMethod(string i) {}
}
";
            var compilationResult = ContractCompiler.Compile(code);

            var assembly = Assembly.Load(compilationResult.Compilation);

            var mapper = new ContractSchemaFactory();

            IDictionary<string, Schema> mapped = mapper.Map(assembly);

            Assert.Equal(1, mapped.Count);
            Assert.True(mapped.ContainsKey("SomeMethod"));
        }
    }
}