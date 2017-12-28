using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractTests
    {



        [Fact]
        public async Task TestSmartContractConsensusLoop()
        {
            // Should be like the constructor of TestContext in 'MinerTests.cs'

            var conensus = new SmartContractConsensusLoop();
            await conensus.StartAsync();

            // await conensus.ValidateAndExecuteBlockAsync();
        }
    }
}
