﻿using System.Threading.Tasks;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ConsensusManagerTests
    {
        [Fact]
        public void BlockMined_PartialValidationOnly_Succeeded_Consensus_TipUpdated()
        {
            TestContext builder = new TestContextBuilder().WithInitialChain(10).BuildOnly();
            ChainedHeader chainTip = builder.InitialChainTip;

            builder.ConsensusRulesEngine.Setup(c => c.GetBlockHashAsync()).Returns(Task.FromResult(chainTip.HashBlock));
            builder.ConsensusManager.InitializeAsync(chainTip).GetAwaiter().GetResult();

            var minedBlock = builder.CreateBlock(chainTip);
            var result = builder.ConsensusManager.BlockMined(minedBlock).GetAwaiter().GetResult();
            Assert.NotNull(result);
            Assert.Equal(minedBlock.GetHash(), builder.ConsensusManager.Tip.HashBlock);
        }
    }
}