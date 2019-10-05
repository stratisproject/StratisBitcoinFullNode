using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR.Compilation;
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

            var mapper = new ContractSwaggerDocMapper();
            var method = assembly.ExportedTypes.First().GetMethod("AcceptsAddress");

            var controllerActionDescriptor = new ControllerActionDescriptor();
            controllerActionDescriptor.MethodInfo = method;
            controllerActionDescriptor.ControllerName = "Test";
            controllerActionDescriptor.DisplayName = "Test Display";
            controllerActionDescriptor.ActionName = "AcceptsAddress";
            

            
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