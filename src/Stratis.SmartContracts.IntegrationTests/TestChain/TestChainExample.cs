using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.TestChain
{
    public class TestChainExample
    {
        [Fact]
        public void UseTestChain()
        {
            using (Test.TestChain chain = new Test.TestChain().Initialize())
            {

            }
        }
    }
}
