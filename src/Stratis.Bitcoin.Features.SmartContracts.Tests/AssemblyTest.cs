using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Cecil;
using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AssemblyTest
    {
        [Fact]
        public void Test()
        {
            byte[] module = File.ReadAllBytes("InfiniteLoop.dll");
            var moduleResult = new ContractModuleDefinitionReader().Read(module);
        }
    }
}
