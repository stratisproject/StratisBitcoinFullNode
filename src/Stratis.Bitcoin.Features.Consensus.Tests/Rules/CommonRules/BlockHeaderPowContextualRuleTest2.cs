using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPowContextualRuleTest2 : TestConsensusRulesUnitTestBase
    {
        public BlockHeaderPowContextualRuleTest2()
        {
            this.network = Network.TestNet; //important for bips
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetLower_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = 5;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111114);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetHigher_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = 5;
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111116);

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldLower_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = 5;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 9));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldEqual_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = 5;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooNew_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = 5;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip34_ThrowsBadVersionConsensusErrorAsync()
        {
            // set height above bip34
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip34_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip66_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 3;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan4_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 3;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan3_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 2;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightHigherThanBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66];
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_BadVersionLowerThan2_HeightSameAsBip65_ThrowsBadVersionConsensusErrorAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 1;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip34_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 1;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip66_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 2;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionHeightBelowBip65_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] - 2;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 3;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_GoodVersionAboveBIPS_DoesNotThrowExceptionAsync()
        {
            this.ruleContext.PreviousHeight = this.consensusRules.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65] + 30;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = new Block();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));
            this.ruleContext.ValidationContext.Block.Header.Version = 4;

            await this.consensusRules.RegisterRule<BlockHeaderPowContextualRule>().RunAsync(this.ruleContext);
        }
    }
}