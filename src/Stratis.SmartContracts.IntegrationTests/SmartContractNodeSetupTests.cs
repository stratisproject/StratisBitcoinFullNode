﻿using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class SmartContractNodeSetupTests
    {
        [Fact]
        public void Mainnet_RequireStandard_False()
        {
            var network = new FakeSmartContractMain();
            Assert.False(network.IsTest());

            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                var node = builder.CreateSmartContractPoANode(network, 0);
                node.Start();
                TestBase.WaitLoop(() => node.State == CoreNodeState.Running);
                Assert.False(node.FullNode.NodeService<MempoolSettings>().RequireStandard);
            }
        }

        private class FakeSmartContractMain : SmartContractsPoARegTest
        {
            public FakeSmartContractMain()
            {
                this.NetworkType = NetworkType.Mainnet;
            }
        }
    }
}
