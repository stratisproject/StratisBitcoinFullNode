using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class StakeValidatorTests : LogsTestBase
    {
        private StakeValidator stakeValidator;
        private Mock<IStakeChain> stakeChain;
        private ConcurrentChain concurrentChain;
        private Mock<ICoinView> coinView;

        public StakeValidatorTests() : base(KnownNetworks.StratisRegTest)
        {
            this.stakeChain = new Mock<IStakeChain>();
            this.concurrentChain = new ConcurrentChain(this.Network);
            this.coinView = new Mock<ICoinView>();

            this.stakeValidator = new StakeValidator(this.Network, this.stakeChain.Object, this.concurrentChain, this.coinView.Object, this.LoggerFactory.Object);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoS_NoPreviousBlockOnChainedHeader_ReturnsSameChainedHeader()
        {
            BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            var chainedHeader = new ChainedHeader(header, header.GetHash(), null);

            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, chainedHeader, true);

            Assert.Equal(chainedHeader, result);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoW_NoPreviousBlockOnChainedHeader_ReturnsSameChainedHeader()
        {
            BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            var chainedHeader = new ChainedHeader(header, header.GetHash(), null);

            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, chainedHeader, false);

            Assert.Equal(chainedHeader, result);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoS_NoPoSBlocksOnChain_ReturnsFirstNonPosHeaderInChain()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            this.stakeChain.Setup(s => s.Get(It.IsAny<uint256>()))
                .Returns(new BlockStake());

            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, headers.Last(), true);

            Assert.Equal(headers.First(), result);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoW_NoPoWBlocksOnChain_ReturnsFirstNonPoWHeaderInChain()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var stakeBlockStake = new BlockStake();
            stakeBlockStake.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            this.stakeChain.Setup(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStake);

            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, headers.Last(), false);

            Assert.Equal(headers.First(), result);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoW_MultiplePoWBlocksOnChain_ReturnsHighestPoWBlockOnChainFromStart()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(3, includePrevBlock: true, network: this.Network);

            var nonStakeBlockStake = new BlockStake();
            var stakeBlockStake = new BlockStake();
            stakeBlockStake.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStake)
                .Returns(stakeBlockStake)
                .Returns(nonStakeBlockStake)
                .Returns(nonStakeBlockStake);

            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, headers.Last(), false);

            Assert.Equal(headers.ElementAt(1), result);
        }

        [Fact]
        public void GetLastPowPosChainedBlock_PoS_MultiplePoSBlocksOnChain_ReturnsHighestPoSBlockOnChainFromStart()
        {

            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(3, includePrevBlock: true, network: this.Network);

            var nonStakeBlockStake = new BlockStake();
            var stakeBlockStake = new BlockStake();
            stakeBlockStake.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(nonStakeBlockStake)
                .Returns(nonStakeBlockStake)
                .Returns(stakeBlockStake)
                .Returns(stakeBlockStake);


            var result = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain.Object, headers.Last(), true);

            Assert.Equal(headers.ElementAt(1), result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesLowerTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesLowerTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesHigherTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesHigherTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesHigherTargetSpacing11_WithinLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_UsesHigherTargetSpacing11_AboveLimit_CalculatesNewTarget()
        {
            Assert.True(false);
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }
    }
}
