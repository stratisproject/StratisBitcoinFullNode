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
        public void Run_BadVersionLowerThan2_HeightHigherThanBip34_ThrowsBadVersionConsensusError()
        {
            // set height above bip34
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan2_HeightSameAsBip34_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            BlockHeader header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan3_HeightHigherThanBip66_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan3_HeightSameAsBip66_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan2_HeightHigherThanBip66_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan2_HeightSameAsBip66_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan4_HeightHigherThanBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan4_HeightSameAsBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan3_HeightHigherThanBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan3_HeightSameAsBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan2_HeightHigherThanBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_BadVersionLowerThan2_HeightSameAsBip65_ThrowsBadVersionConsensusError()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1);

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public void Run_GoodVersionHeightBelowBip34_DoesNotThrowException()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 1;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 2);

            this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void Run_GoodVersionHeightBelowBip66_DoesNotThrowException()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 2;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 2);

            this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void Run_GoodVersionHeightBelowBip65_DoesNotThrowException()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 3;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 2);

            this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void Run_GoodVersionAboveBIPS_DoesNotThrowException()
        {
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.CreateBlock();
            header.Bits = new Target(0x1f111115);
            header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            header.Version = 4;

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] + 30);

            this.consensusRules.RegisterRule<BitcoinActivationRule>().Run(this.ruleContext);
        }
    }
}