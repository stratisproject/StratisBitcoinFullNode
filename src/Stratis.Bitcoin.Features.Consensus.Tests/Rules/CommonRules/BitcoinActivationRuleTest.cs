using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BitcoinActivationRuleTest : TestConsensusRulesUnitTestBase
    {
        public BitcoinActivationRuleTest()
        {
            this.network = KnownNetworks.TestNet; //important for bips
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip34_ThrowsBadVersionConsensusErrorAsync()
        {
            // set height above bip34
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;
            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip34_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip34_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            await this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip66_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            await this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip65_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            await this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionAboveBIPS_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.ConsensusTipHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] + 30;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 4;

            this.ruleContext.ValidationContext.ChainedHeader = new ChainedHeader(header, header.GetHash(), 1);

            await this.consensusRules.RegisterRule<BitcoinActivationRule>().RunAsync(this.ruleContext);
        }
    }
}