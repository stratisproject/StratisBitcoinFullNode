using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR.Compilation;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class ContractSwaggerDocMapperTests
    {
        [Fact]
        public void Map_Parameter_Type_Success()
        {
            var code = @"
using Stratis.SmartContracts;

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

    public void AcceptsAllParams(bool b, byte bb, byte[] ba, char c, string s, uint ui, ulong ul, int i, long l, Address a) {}
}
";
            var compilationResult = ContractCompiler.Compile(code);
            Assert.True(compilationResult.Success);

            var assembly = Assembly.Load(compilationResult.Compilation);

            var mapper = new ContractSwaggerDocMapper("test");

            MethodInfo methodInfo = assembly.ExportedTypes.First().GetMethod("AcceptsAllParams");
            Schema mapped = mapper.Map(methodInfo);
            var properties = mapped.Properties;

            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(bool)]().Type, properties["b"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(byte)]().Type, properties["bb"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(byte[])]().Type, properties["ba"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(char)]().Type, properties["c"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(string)]().Type, properties["s"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(uint)]().Type, properties["ui"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(ulong)]().Type, properties["ul"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(int)]().Type, properties["i"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(long)]().Type, properties["l"].Type);
            Assert.Equal(ContractSwaggerDocMapper.PrimitiveTypeMap[typeof(string)]().Type, properties["a"].Type);
        }

        [Fact]
        public void Map_Return_Type_Success()
        {
            // TODO do we need to worry about the return type?
        }

        [Fact]
        public void Only_Map_Deployed_Type_Success()
        {

        }
    }
}