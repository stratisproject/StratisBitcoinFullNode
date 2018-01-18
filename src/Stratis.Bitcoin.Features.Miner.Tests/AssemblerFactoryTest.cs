using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class AssemblerFactoryTest : LogsTestBase
    {
        [Fact]
        public void PowAssemblerFactory_Create_ReturnsPowBlockAssembler()
        {
            var network = Network.StratisTest;
            var options = network.Consensus.Options;
            try
            {
                var chain = new ConcurrentChain();
                network.Consensus.Options = new PowConsensusOptions();

                var factory = new PowAssemblerFactory(new Mock<IConsensusLoop>().Object, network, new MemoryPool.MempoolSchedulerLock(), new Mock<ITxMempool>().Object,
                    new Mock<IDateTimeProvider>().Object, this.LoggerFactory.Object, null);

                var result = factory.Create(chain.Tip, null);

                Assert.IsType<PowBlockAssembler>(result);
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                network.Consensus.Options = options;
            }
        }

        [Fact]
        public void PosAssemblerFactory_Create_ReturnsPosBlockAssembler()
        {
            var network = Network.StratisTest;
            var options = network.Consensus.Options;

            try
            {
                var chain = new ConcurrentChain();
                network.Consensus.Options = new PosConsensusOptions();

                var factory = new PosAssemblerFactory(new Mock<IConsensusLoop>().Object, network, new MemoryPool.MempoolSchedulerLock(), new Mock<ITxMempool>().Object,
                    new Mock<IStakeValidator>().Object, new Mock<IDateTimeProvider>().Object, this.LoggerFactory.Object, null);

                var result = factory.Create(chain.Tip, null);

                Assert.IsType<PosBlockAssembler>(result);
            }
            finally
            {
                // This is a static in the global context so be careful updating it. I'm resetting it after being done testing so I don't influence other tests.
                network.Consensus.Options = options;
            }
        }
    }
}
