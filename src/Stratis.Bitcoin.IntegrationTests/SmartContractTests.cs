using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractTests
    {
        public class TestContext
        {
            public SCConsensusLoop Consensus { get; set; }
            public Network Network { get; set; }
            public SCChain Chain { get; set; }

            public async Task InitializeAsync()
            {
                this.Network = Network.Main;
                this.Chain = new SCChain(this.Network);
                this.Consensus = new SCConsensusLoop(this.Chain);

                // will need to be able to reuse all of the networking, mempool, etc.

                // for now, need to build the consensus loop assuming that it can get everything it
                // needs from elsewhere

                var blockValidationContext = new SCBlockValidationContext();

                await this.Consensus.AcceptBlockAsync(blockValidationContext);





            }
        }

        [Fact]
        public async Task TestSmartContractConsensusLoop()
        {
            TestContext context = new TestContext();
            await context.InitializeAsync();
        }
    }
}
