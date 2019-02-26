using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Policy;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class StakeValidatorTests : LogsTestBase
    {
        private StakeValidator stakeValidator;
        private readonly Mock<IStakeChain> stakeChain;
        private readonly ConcurrentChain concurrentChain;
        private readonly Mock<ICoinView> coinView;
        private readonly Mock<IConsensus> consensus;

        public StakeValidatorTests() : base(new StratisRegTest())
        {
            this.stakeChain = new Mock<IStakeChain>();
            this.concurrentChain = new ConcurrentChain(this.Network);
            this.coinView = new Mock<ICoinView>();
            this.consensus = new Mock<IConsensus>();
            this.stakeValidator = CreateStakeValidator();
        }

        private StakeValidator CreateStakeValidator()
        {
            return new StakeValidator(this.Network, this.stakeChain.Object, this.concurrentChain, this.coinView.Object, this.LoggerFactory.Object);
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
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_ElevenTimesHigherTargetSpacing_UsesTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockBeforeSecondBlock_ElevenTimesHigherTargetSpacing_UsesTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockSameTimeAsSecondBlock_UsesTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = firstBlockTime;
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockSameTimeAsSecondBlock_UsesTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var secondBlockTime = firstBlockTime;
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(firstBlockTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesLowerTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));
            var expectedTarget = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000")); // 1.66667751753

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesLowerTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000")); // 1.66667751753

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesHigherTargetSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));
            var expectedTarget = new Target(new uint256("000000011ffe0000000000000000000000000000000000000000000000000000")); // 0.888899438461

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_UsesHigherTargetSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 2)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_ElevenTimesHigherTargetSpacing_LowersActualSpacing_WithinLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Multiply(BigInteger.ValueOf(2));
            var expectedTarget = new Target(new uint256("00000001fffe0000000000000000000000000000000000000000000000000000")); // 0.5

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CalculateRetarget_FirstBlockAfterSecondBlock_ElevenTimesHigherTargetSpacing_LowersActualSpacing_AboveLimit_CalculatesNewTarget()
        {
            var now = DateTime.UtcNow;
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds * 11)));
            var targetLimit = Target.Difficulty1.ToBigInteger().Subtract(BigInteger.ValueOf(1));
            var expectedTarget = new Target(targetLimit);

            var result = this.stakeValidator.CalculateRetarget(firstBlockTime, firstBlockTarget, secondBlockTime, targetLimit);

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_NoChainedHeaderProvided_ReturnsConsensusPowLimit()
        {
            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, null, this.consensus.Object, true);

            Assert.Equal(powLimit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoW_NoChainedHeaderProvided_ReturnsConsensusPowLimit()
        {
            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, null, this.consensus.Object, false);

            Assert.Equal(powLimit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoW_FirstBlock_NoPreviousPoWBlock_ReturnsPowLimit()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var stakeBlockStake = new BlockStake();
            stakeBlockStake.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            this.stakeChain.Setup(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStake);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);


            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, false);

            Assert.Equal(powLimit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_FirstBlock_NoPreviousPoSBlock_ReturnsPosLimitV2()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var stakeBlockStake = new BlockStake();
            this.stakeChain.Setup(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStake);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var posV2Limit = new Target(new uint256("00000011efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.ProofOfStakeLimitV2)
                .Returns(posV2Limit.ToBigInteger());

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, true);

            Assert.Equal(posV2Limit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoW_SecondBlock_NoPreviousPoWBlock_ReturnsPowLimit()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);


            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, false);

            Assert.Equal(powLimit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_SecondBlock_NoPreviousPoSBlock_ReturnsPosLimitV2()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var posV2Limit = new Target(new uint256("00000011efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.ProofOfStakeLimitV2)
                .Returns(posV2Limit.ToBigInteger());

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, true);

            Assert.Equal(posV2Limit, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_BlocksExist_PosNoRetargetEnabled_ReturnsFirstBlockHeaderBits()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);
            IncrementHeaderBits(headers, Target.Difficulty1);

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePos) // should be returned
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var posV2Limit = new Target(new uint256("00000011efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.ProofOfStakeLimitV2)
                .Returns(posV2Limit.ToBigInteger());

            this.consensus.Setup(c => c.PosNoRetargeting).Returns(true);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, true);

            var expectedTarget = headers.Last().Previous.Previous.Header.Bits;

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoW_BlocksExist_PowNoRetargetDisabled_CalculatesRetarget()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var firstBlock = headers.Last().Previous;
            var now = DateTime.UtcNow;
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            firstBlock.Header.Time = firstBlockTime;
            firstBlock.Header.Bits = firstBlockTarget;
            firstBlock.Previous.Header.Time = secondBlockTime;

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow) // should be used in calculations
                .Returns(stakeBlockStakePow) // should be used in calculations
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos);

            var powLimit = new Target(headers.Last().Header.Bits.ToBigInteger().Add(Target.Difficulty1.ToBigInteger()).Add(Target.Difficulty1.ToBigInteger()));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            this.consensus.Setup(c => c.PowNoRetargeting)
                .Returns(false);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, false);

            var expectedTarget = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000")); // 1.66667751753

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_BlocksExist_PosNoRetargetDisabled_CalculatesRetarget()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);

            var firstBlock = headers.Last().Previous.Previous;
            var now = DateTime.UtcNow;
            var firstBlockTarget = Target.Difficulty1;
            var firstBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now.AddSeconds(StakeValidator.TargetSpacingSeconds / 2)));
            var secondBlockTime = Utils.DateTimeToUnixTime(new DateTimeOffset(now));
            firstBlock.Header.Time = firstBlockTime;
            firstBlock.Header.Bits = firstBlockTarget;
            firstBlock.Previous.Header.Time = secondBlockTime;

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePos) // should be returned
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePow);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            var posV2Limit = new Target(headers.Last().Header.Bits.ToBigInteger().Add(Target.Difficulty1.ToBigInteger()).Add(Target.Difficulty1.ToBigInteger()));
            this.consensus.Setup(c => c.ProofOfStakeLimitV2)
                .Returns(posV2Limit.ToBigInteger());

            this.consensus.Setup(c => c.PosNoRetargeting).Returns(false);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, true);

            var expectedTarget = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000")); // 1.66667751753

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void CheckProofOfStake_TransactionNotCoinStake_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = new Transaction();
            Assert.False(transaction.IsCoinStake);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));

            Assert.Equal(ConsensusErrors.NonCoinstake.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_UTXONotInUnspentOutputSet_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction();
            Assert.True(transaction.IsCoinStake);

            var context = new PosRuleContext()
            {
                UnspentOutputSet = new UnspentOutputSet()
            };
            context.UnspentOutputSet.SetCoins(new UnspentOutputs[0]);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(context, chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_InvalidSignature_ThrowsConsensusError()
        {
            Transaction previousTx = this.Network.CreateTransaction();
            previousTx.AddOutput(new TxOut());

            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction(previousTx);
            Assert.True(transaction.IsCoinStake);

            var unspentoutputs = new UnspentOutputs[]
            {
                new UnspentOutputs(transaction.Inputs[0].PrevOut.Hash, new NBitcoin.BitcoinCore.Coins(transaction, 15))
                {
                    Outputs = new TxOut[] { new TxOut() }
                }
            };

            var context = new PosRuleContext()
            {
                UnspentOutputSet = new UnspentOutputSet()
            };
            context.UnspentOutputSet.SetCoins(unspentoutputs);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(context, chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.CoinstakeVerifySignatureFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_InvalidStakeDepth_ThrowsConsensusError()
        {
            var keystore = new CKeyStore();
            var key1 = new Key(true);
            var key2 = new Key(true);
            PubKey pubkey1 = key1.PubKey;
            PubKey pubkey2 = key2.PubKey;
            keystore.AddKeyPubKey(key1, pubkey1);
            keystore.AddKeyPubKey(key2, pubkey2);
            Script scriptPubkey1 = new Script(Op.GetPushOp(pubkey1.ToBytes()), OpcodeType.OP_CHECKSIG);
            Script scriptPubkey2 = new Script(Op.GetPushOp(pubkey2.ToBytes()), OpcodeType.OP_CHECKSIG);
            keystore.AddCScript(scriptPubkey1);
            keystore.AddCScript(scriptPubkey2);
            Transaction output1 = this.Network.CreateTransaction();
            Transaction input1 = this.Network.CreateTransaction();

            // Normal pay-to-compressed-pubkey.
            CreateCreditAndSpend(keystore, scriptPubkey1, ref output1, ref input1);

            var unspentoutputs = new UnspentOutputs(output1.GetHash(), new NBitcoin.BitcoinCore.Coins(output1, 1));

            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            Assert.True(input1.IsCoinStake);

            var context = new PosRuleContext()
            {
                UnspentOutputSet = new UnspentOutputSet()
            };
            context.UnspentOutputSet.SetCoins(new UnspentOutputs[] { unspentoutputs });

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(context, chainedHeader, new BlockStake(), input1, 15));

            Assert.Equal(ConsensusErrors.InvalidStakeDepth.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_ValidStake_DoesNotThrowConsensusError()
        {
            var keystore = new CKeyStore();
            var key1 = new Key(true);
            var key2 = new Key(true);
            PubKey pubkey1 = key1.PubKey;
            PubKey pubkey2 = key2.PubKey;
            keystore.AddKeyPubKey(key1, pubkey1);
            keystore.AddKeyPubKey(key2, pubkey2);
            Script scriptPubkey1 = new Script(Op.GetPushOp(pubkey1.ToBytes()), OpcodeType.OP_CHECKSIG);
            Script scriptPubkey2 = new Script(Op.GetPushOp(pubkey2.ToBytes()), OpcodeType.OP_CHECKSIG);
            keystore.AddCScript(scriptPubkey1);
            keystore.AddCScript(scriptPubkey2);
            Transaction output1 = this.Network.CreateTransaction();
            Transaction input1 = this.Network.CreateTransaction();

            var satoshis = 5000 * Money.COIN;

            // Normal pay-to-compressed-pubkey.
            CreateCreditAndSpend(keystore, scriptPubkey1, ref output1, ref input1, output1Satoshis: satoshis);

            var unspentoutputs = new UnspentOutputs(output1.GetHash(), new NBitcoin.BitcoinCore.Coins(output1, 1));
            var chainedHeader = ChainedHeadersHelper.CreateConsecutiveHeaders(15).Last();

            Assert.True(input1.IsCoinStake);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(new UnspentOutputs[] { unspentoutputs }, uint256.One));

            var headerbits = Target.Difficulty1.ToCompact();
            var blockStake = new BlockStake() { StakeModifierV2 = uint256.Zero };

            var context = new PosRuleContext()
            {
                UnspentOutputSet = new UnspentOutputSet()
            };
            context.UnspentOutputSet.SetCoins(new UnspentOutputs[] { unspentoutputs });

            this.stakeValidator.CheckProofOfStake(context, chainedHeader, blockStake, input1, headerbits);
        }

        [Fact]
        public void CheckStakeKernelHash_TransactionTimeBeforeStakeTime_ThrowsConsensusError()
        {
            var transactionTimestamp = DateTime.Now;
            var posTimeStamp = transactionTimestamp.AddSeconds(-1);
            var transaction = this.Network.CreateTransaction();
            transaction.Time = Utils.DateTimeToUnixTime(transactionTimestamp);
            uint transactionTime = Utils.DateTimeToUnixTime(posTimeStamp);
            UnspentOutputs stakingCoins = new UnspentOutputs(15, transaction);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckStakeKernelHash(new PosRuleContext(), 0, uint256.Zero, stakingCoins, new OutPoint(), transactionTime));

            Assert.Equal(ConsensusErrors.StakeTimeViolation.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckStakeKernelHash_InvalidKernelHashTarget_ThrowsConsensusError()
        {
            var transactionTimestamp = DateTime.Now;
            var posTimeStamp = transactionTimestamp;
            var transaction = CreateStubCoinStakeTransaction();
            transaction.Time = Utils.DateTimeToUnixTime(transactionTimestamp);
            uint transactionTime = Utils.DateTimeToUnixTime(posTimeStamp);
            UnspentOutputs stakingCoins = new UnspentOutputs(15, transaction);
            var outpoint = new OutPoint(transaction, 1);

            var result = this.stakeValidator.CheckStakeKernelHash(new PosRuleContext(), 0, uint256.Zero, stakingCoins, outpoint, transactionTime);

            Assert.False(result);
        }

        [Fact]
        public void CheckStakeKernelHash_ValidKernelHash_DoesNotThrowException()
        {
            var transactionTimestamp = DateTime.Now;
            var posTimeStamp = transactionTimestamp;

            var transaction = CreateStubCoinStakeTransaction(5000 * Money.COIN);
            transaction.Time = Utils.DateTimeToUnixTime(transactionTimestamp);
            uint transactionTime = Utils.DateTimeToUnixTime(posTimeStamp);
            UnspentOutputs stakingCoins = new UnspentOutputs(15, transaction);
            var outpoint = new OutPoint(transaction, 1);
            var headerbits = Target.Difficulty1.ToCompact();

            this.stakeValidator.CheckStakeKernelHash(new PosRuleContext(), headerbits, uint256.Zero, stakingCoins, outpoint, transactionTime);
        }

        [Fact]
        public void CheckStakeKernelHash_TransactionTimeSameAsStakeTime_ValidStakeHashTarget_DoesNotThrowException()
        {
            var transactionTimestamp = DateTime.Now;
            var posTimeStamp = transactionTimestamp;

            var transaction = CreateStubCoinStakeTransaction(5000 * Money.COIN);
            transaction.Time = Utils.DateTimeToUnixTime(transactionTimestamp);
            uint transactionTime = Utils.DateTimeToUnixTime(posTimeStamp);
            UnspentOutputs stakingCoins = new UnspentOutputs(15, transaction);
            var outpoint = new OutPoint(transaction, 1);
            var headerbits = Target.Difficulty1.ToCompact();

            this.stakeValidator.CheckStakeKernelHash(new PosRuleContext(), headerbits, uint256.Zero, stakingCoins, outpoint, transactionTime);
        }

        [Fact]
        public void CheckStakeKernelHash_TransactionTimeAboveStakeTime_ValidStakeHashTarget_DoesNotThrowException()
        {
            var transactionTimestamp = DateTime.Now;
            var posTimeStamp = transactionTimestamp.AddSeconds(1);

            var transaction = CreateStubCoinStakeTransaction(5000 * Money.COIN);
            transaction.Time = Utils.DateTimeToUnixTime(transactionTimestamp);
            uint transactionTime = Utils.DateTimeToUnixTime(posTimeStamp);
            UnspentOutputs stakingCoins = new UnspentOutputs(15, transaction);
            var outpoint = new OutPoint(transaction, 1);
            var headerbits = Target.Difficulty1.ToCompact();

            this.stakeValidator.CheckStakeKernelHash(new PosRuleContext(), headerbits, uint256.Zero, stakingCoins, outpoint, transactionTime);
        }

        [Fact]
        public void ComputeStakeModifierV2_PrevChainedHeaderNull_ReturnsZero()
        {
            var result = this.stakeValidator.ComputeStakeModifierV2(null, uint256.Zero, uint256.One);

            Assert.Equal(uint256.Zero, result);
        }

        [Fact]
        public void ComputeStakeModifierV2_UsingBlockStakeAndKernel_CalculatesStakeModifierHash()
        {
            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), 1273671, uint256.One);

            Assert.Equal(new uint256("9a37d63a1cddaeb9b018d24f05020d46945b0292f5642cbbcf3b204a14d3748d"), result);
        }

        [Fact]
        public void ComputeStakeModifierV2_UsingChangedBlockStakeAndKernel_CalculatesStakeModifierHash()
        {
            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), 12, uint256.One);

            Assert.Equal(new uint256("f9a82ef89e0bf841dd9a6b5cea0131a61ea3e2e4a3d1ab56eca5a8ee4da1dade"), result);
        }

        [Fact]
        public void ComputeStakeModifierV2_UsingBlockStakeAndChangedKernel_CalculatesStakeModifierHash()
        {
            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), 1273671, new uint256(2));

            Assert.Equal(new uint256("8e6316364421f6afe6d18a799e2643b4226f0ca3d60e9a71f6064908aafbe65a"), result);
        }

        [Fact]
        public void CheckKernel_CoinsNotInCoinView_ThrowsConsensusError()
        {

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
             .ReturnsAsync((FetchCoinsResponse)null);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_LessThanOneCoinsInCoinView_ThrowsConsensusError()
        {

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(new UnspentOutputs[0], uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_MoreThanOneCoinsInCoinView_ThrowsConsensusError()
        {

            var unspentoutputs = new UnspentOutputs[]
            {
                new UnspentOutputs(),
                new UnspentOutputs(),
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_SingleNullValueCoinInCoinView_ThrowsConsensusError()
        {
            var header = this.AppendBlock(null, this.concurrentChain);

            var unspentoutputs = new UnspentOutputs[]
            {
                null
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, header.HashBlock));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_PrevBlockNotFoundOnConcurrentChain_ThrowsConsensusError()
        {
            var unspentoutputs = new UnspentOutputs[]
            {
                new UnspentOutputs()
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_TargetDepthNotMet_ThrowsConsensusError()
        {
            var header = this.AppendBlock(null, this.concurrentChain);
            var transaction = CreateStubCoinStakeTransaction();
            header.Block.Transactions.Add(transaction);

            var unspentoutputs = new UnspentOutputs[]
            {
                new UnspentOutputs((uint)header.Height, header.Block.Transactions[0])
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, header.HashBlock));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.InvalidStakeDepth.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_InvalidStakeBlock_ThrowsConsensusError()
        {
            var header = this.CreateChainWithStubCoinStakeTransactions(this.concurrentChain, 30);
            ChainedHeader stakableHeader = null;
            for (int i = 0; i < 15; i++)
            {
                stakableHeader = stakableHeader == null ? header.Previous : stakableHeader.Previous;
            }

            var unspentoutputs = new UnspentOutputs[]
            {
                new UnspentOutputs((uint)stakableHeader.Height, stakableHeader.Block.Transactions[0])
            };

            this.stakeChain.Setup(s => s.Get(header.HashBlock))
                .Returns((BlockStake)null);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, header.HashBlock));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), header, 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.BadStakeBlock.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_ValidKernelCheck_DoesNotThrowConsensusError()
        {
            var satoshis = 5000 * Money.COIN;
            var header = this.CreateChainWithStubCoinStakeTransactions(this.concurrentChain, 30, satoshis);
            ChainedHeader stakableHeader = null;
            for (int i = 0; i < 15; i++)
            {
                stakableHeader = stakableHeader == null ? header.Previous : stakableHeader.Previous;
            }

            var transaction = stakableHeader.Block.Transactions[1];
            var blockStake = new BlockStake() { StakeModifierV2 = uint256.Zero };
            this.stakeChain.Setup(s => s.Get(header.HashBlock))
                .Returns(blockStake);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(new UnspentOutputs[]
            {
                new UnspentOutputs((uint)stakableHeader.Height, transaction)
            }, header.HashBlock));


            var outPoint = new OutPoint(transaction, 1);
            var headerbits = Target.Difficulty1.ToCompact();
            var transactionTime = stakableHeader.Block.Transactions[0].Time;

            this.stakeValidator.CheckKernel(new PosRuleContext(), header, headerbits, transactionTime, outPoint);
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthSmallerThanTargetDepth_ReturnsTrue()
        {
            var trans = CreateStubCoinStakeTransaction();
            var referenceHeader = ChainedHeadersHelper.CreateConsecutiveHeaders(18).Last();

            var coins = new UnspentOutputs((uint)9, trans);
            var targetDepth = 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.True(result);
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthEqualToTargetDepth_ReturnsFalse()
        {
            var trans = CreateStubCoinStakeTransaction();
            var referenceHeader = ChainedHeadersHelper.CreateConsecutiveHeaders(18).Last();

            var coins = new UnspentOutputs((uint)8, trans);
            var targetDepth = 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.False(result);
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthHigherThanTargetDepth_ReturnsFalse()
        {
            var trans = CreateStubCoinStakeTransaction();
            var referenceHeader = ChainedHeadersHelper.CreateConsecutiveHeaders(18).Last();

            var coins = new UnspentOutputs((uint)7, trans);
            var targetDepth = 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxToInN_OutSideTxToInputRangeLower_ReturnsFalse()
        {
            var coin = new UnspentOutputs() { };
            var txTo = this.Network.CreateTransaction();

            var result = this.stakeValidator.VerifySignature(coin, txTo, -1, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxToInN_OutSideTxToInputRangeHigher_ReturnsFalse()
        {
            var coin = new UnspentOutputs() { };
            var txTo = this.Network.CreateTransaction();
            txTo.Inputs.Add(new TxIn() { });

            var result = this.stakeValidator.VerifySignature(coin, txTo, txTo.Inputs.Count, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxInputPrevoutHigherThanCoinOutputLength_ReturnsFalse()
        {
            var coin = new UnspentOutputs()
            {
                Outputs = new TxOut[]
                {
                    new TxOut()
                }

            };
            var txTo = this.Network.CreateTransaction();
            txTo.Inputs.Add(new TxIn() { PrevOut = new OutPoint(txTo, 2) });

            var result = this.stakeValidator.VerifySignature(coin, txTo, 0, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxInputPrevoutSameAsCoinOutputLength_ReturnsFalse()
        {
            var coin = new UnspentOutputs()
            {
                Outputs = new TxOut[]
               {
                    new TxOut()
               }

            };
            var txTo = this.Network.CreateTransaction();
            txTo.Inputs.Add(new TxIn() { PrevOut = new OutPoint(txTo, 1) });

            var result = this.stakeValidator.VerifySignature(coin, txTo, 0, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxInputPrevoutLowerThanZero_ReturnsFalse()
        {
            var txTo = this.Network.CreateTransaction();

            var coin = new UnspentOutputs(12, txTo)
            {
                Outputs = new TxOut[]
               {
                    new TxOut(),
                    new TxOut()
               },

            };

            txTo.Inputs.Add(new TxIn() { PrevOut = new OutPoint(txTo, -1) });

            var result = this.stakeValidator.VerifySignature(coin, txTo, 0, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_TxInputPrevOutHashDoesNotMatchCoinTransactionId_ReturnsFalse()
        {
            var txTo = this.Network.CreateTransaction();

            var coin = new UnspentOutputs(12, txTo)
            {
                Outputs = new TxOut[]
               {
                    new TxOut(),
                    new TxOut()
               },

            };

            txTo.Inputs.Add(new TxIn() { PrevOut = new OutPoint(new uint256(125), 1) });

            var result = this.stakeValidator.VerifySignature(coin, txTo, 0, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_InvalidSignature_ReturnsFalse()
        {
            var key = new Key();
            Script scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);
            Transaction tx = this.Network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 0))
            {
                ScriptSig = scriptPubKey
            });
            tx.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1))
            {
                ScriptSig = scriptPubKey
            });
            tx.AddOutput(new TxOut("21", key.PubKey.Hash));

            var coin = new Coin(new OutPoint(tx, 2), tx.Outputs[0]);
            tx.Sign(this.Network, key, coin);

            var unspentOutputs = new UnspentOutputs(tx.Inputs[0].PrevOut.Hash, new NBitcoin.BitcoinCore.Coins(tx, 1));
            var result = this.stakeValidator.VerifySignature(unspentOutputs, tx, 0, ScriptVerify.None);

            Assert.False(result);
        }

        [Fact]
        public void VerifySignature_ValidSignature_ReturnsTrue()
        {
            var keystore = new CKeyStore();
            var key1 = new Key(true);
            var key2 = new Key(true);
            PubKey pubkey1 = key1.PubKey;
            PubKey pubkey2 = key2.PubKey;
            keystore.AddKeyPubKey(key1, pubkey1);
            keystore.AddKeyPubKey(key2, pubkey2);
            Script scriptPubkey1 = new Script(Op.GetPushOp(pubkey1.ToBytes()), OpcodeType.OP_CHECKSIG);
            Script scriptPubkey2 = new Script(Op.GetPushOp(pubkey2.ToBytes()), OpcodeType.OP_CHECKSIG);
            keystore.AddCScript(scriptPubkey1);
            keystore.AddCScript(scriptPubkey2);
            Transaction output1 = this.Network.CreateTransaction();
            Transaction input1 = this.Network.CreateTransaction();

            // Normal pay-to-compressed-pubkey.
            CreateCreditAndSpend(keystore, scriptPubkey1, ref output1, ref input1);

            var unspentOutputs = new UnspentOutputs(output1.GetHash(), new NBitcoin.BitcoinCore.Coins(output1, 1));

            var result = this.stakeValidator.VerifySignature(unspentOutputs, input1, 0, ScriptVerify.Standard);

            Assert.True(result);
        }

        [Fact]
        public void GetTargetDepthRequired_Testnet_HeightBelowMinConfirmationHeight_UsesChainedHeaderHeightAndConsensusOptions_CalculatesTarget()
        {
            this.Network = KnownNetworks.StratisTest;
            this.stakeValidator = CreateStakeValidator();

            var height = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightTestnet - 2;
            BlockHeader blockHeader = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            ChainedHeader header = new ChainedHeader(blockHeader, uint256.One, height);

            var depth = this.stakeValidator.GetTargetDepthRequired(header);

            Assert.Equal(9, depth);
        }

        [Fact]
        public void GetTargetDepthRequired_Testnet_HeightAtMinConfirmationHeight_UsesChainedHeaderHeightAndConsensusOptions_CalculatesTarget()
        {
            this.Network = KnownNetworks.StratisTest;
            this.stakeValidator = CreateStakeValidator();

            var height = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightTestnet - 1;
            BlockHeader blockHeader = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            ChainedHeader header = new ChainedHeader(blockHeader, uint256.One, height);

            var depth = this.stakeValidator.GetTargetDepthRequired(header);

            Assert.Equal(19, depth);
        }

        [Fact]
        public void GetTargetDepthRequired_Mainnet_HeightBelowMinConfirmationHeight_UsesChainedHeaderHeightAndConsensusOptions_CalculatesTarget()
        {
            this.Network = KnownNetworks.StratisMain;
            this.stakeValidator = CreateStakeValidator();

            var height = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightMainnet - 2;
            BlockHeader blockHeader = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            ChainedHeader header = new ChainedHeader(blockHeader, uint256.One, height);

            var depth = this.stakeValidator.GetTargetDepthRequired(header);

            Assert.Equal(49, depth);
        }

        [Fact]
        public void GetTargetDepthRequired_Mainnet_HeightAtMinConfirmationHeight_UsesChainedHeaderHeightAndConsensusOptions_CalculatesTarget()
        {
            this.Network = KnownNetworks.StratisMain;
            this.stakeValidator = CreateStakeValidator();

            var height = PosConsensusOptions.CoinstakeMinConfirmationActivationHeightMainnet - 1;
            BlockHeader blockHeader = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
            ChainedHeader header = new ChainedHeader(blockHeader, uint256.One, height);

            var depth = this.stakeValidator.GetTargetDepthRequired(header);

            Assert.Equal(499, depth);
        }

        private ChainedHeader CreateChainWithStubCoinStakeTransactions(ConcurrentChain chain, int height, Money money = null)
        {
            ChainedHeader previous = null;
            uint nonce = RandomUtils.GetUInt32();
            for (int i = 0; i < height; i++)
            {
                Block block = this.Network.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                block.Transactions.Add(CreateStubCoinStakeTransaction(money));

                if (!chain.TrySetTip(block.Header, out previous))
                    throw new InvalidOperationException("Previous not existing");

                previous.Block = block;
            }

            return previous;
        }

        private ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.Network.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");

                last.Block = block;
            }
            return last;
        }

        private Transaction CreateStubCoinStakeTransaction(Money outputValue = null)
        {
            Transaction previousTx = this.Network.CreateTransaction();
            previousTx.AddOutput(new TxOut());

            return CreateStubCoinStakeTransaction(previousTx, outputValue);
        }

        private Transaction CreateStubCoinStakeTransaction(Transaction previousTx, Money outputValue = null)
        {
            Transaction coinstakeTx = this.Network.CreateTransaction();
            coinstakeTx.AddOutput(new TxOut(0, Script.Empty));
            if (outputValue != null)
            {
                coinstakeTx.AddOutput(new TxOut(outputValue, new Script()));
            }
            else
            {
                coinstakeTx.AddOutput(new TxOut(new Money(50), new Script()));
            }

            coinstakeTx.AddInput(previousTx, 0);

            return coinstakeTx;
        }

        private static void IncrementHeaderBits(List<ChainedHeader> headers, Target incrementTarget)
        {
            foreach (var header in headers)
            {
                if (header.Previous != null)
                {
                    header.Header.Bits = new Target(header.Previous.Header.Bits.ToBigInteger().Add(incrementTarget.ToBigInteger()));
                }
            }
        }

        private class CKeyStore
        {
            internal List<Tuple<Key, PubKey>> _Keys = new List<Tuple<Key, PubKey>>();
            internal List<Script> _Scripts = new List<Script>();
            internal void AddKeyPubKey(Key key, PubKey pubkey)
            {
                this._Keys.Add(Tuple.Create(key, pubkey));
            }

            internal void AddCScript(Script scriptPubkey)
            {
                this._Scripts.Add(scriptPubkey);
            }
        }

        private void CreateCreditAndSpend(CKeyStore keystore, Script outscript, ref Transaction output, ref Transaction input, bool success = true, Money output1Satoshis = null)
        {
            CreateCreditAndSpend(keystore, outscript, ref output, ref input, DateTime.Now, success, output1Satoshis);
        }

        private void CreateCreditAndSpend(CKeyStore keystore, Script outscript, ref Transaction output, ref Transaction input, DateTime posTimeStamp, bool success = true, Money output1Satoshis = null)
        {
            Transaction outputm = this.Network.CreateTransaction();
            outputm.Version = 1;
            outputm.Time = Utils.DateTimeToUnixTime(posTimeStamp);
            outputm.Inputs.Add(new TxIn());
            outputm.Inputs[0].PrevOut = new OutPoint();
            outputm.Inputs[0].ScriptSig = Script.Empty;
            outputm.Inputs[0].WitScript = new WitScript();
            outputm.Outputs.Add(new TxOut());
            if (output1Satoshis != null)
            {
                outputm.Outputs[0].Value = output1Satoshis;
            }
            else
            {
                outputm.Outputs[0].Value = Money.Satoshis(1);
            }
            outputm.Outputs[0].ScriptPubKey = outscript;

            output = this.Network.CreateTransaction(outputm.ToBytes());

            Assert.True(output.Inputs.Count == 1);
            Assert.True(output.Inputs[0].ToBytes().SequenceEqual(outputm.Inputs[0].ToBytes()));
            Assert.True(output.Outputs.Count == 1);
            Assert.True(output.Inputs[0].ToBytes().SequenceEqual(outputm.Inputs[0].ToBytes()));
            Assert.True(!output.HasWitness);

            Transaction inputm = this.Network.CreateTransaction();
            inputm.Version = 1;
            outputm.Time = Utils.DateTimeToUnixTime(posTimeStamp);
            inputm.Inputs.Add(new TxIn());
            inputm.Inputs[0].PrevOut.Hash = output.GetHash();
            inputm.Inputs[0].PrevOut.N = 0;
            inputm.Inputs[0].WitScript = new WitScript();


            inputm.Outputs.Add(new TxOut(0, Script.Empty));
            inputm.Outputs.Add(new TxOut(Money.Satoshis(1), Script.Empty));
            bool ret = SignSignature(keystore, output, inputm);
            Assert.True(ret == success, "couldn't sign");


            input = this.Network.CreateTransaction(inputm.ToBytes());
            Assert.True(input.Inputs.Count == 1);
            Assert.True(input.Inputs[0].ToBytes().SequenceEqual(inputm.Inputs[0].ToBytes()));
            Assert.True(input.Outputs.Count == 2);
            Assert.True(input.Outputs[0].ToBytes().SequenceEqual(inputm.Outputs[0].ToBytes()));
            Assert.True(input.Outputs[1].ToBytes().SequenceEqual(inputm.Outputs[1].ToBytes()));
            Assert.True(input.IsCoinStake);
        }

        private bool SignSignature(CKeyStore keystore, Transaction txFrom, Transaction txTo)
        {
            TransactionBuilder builder = CreateBuilder(this.Network, keystore, txFrom);
            builder.SignTransactionInPlace(txTo);
            return builder.Verify(txTo);
        }

        private static TransactionBuilder CreateBuilder(Network network, CKeyStore keystore, Transaction txFrom)
        {
            Coin[] coins = txFrom.Outputs.AsCoins().ToArray();
            TransactionBuilder builder = new TransactionBuilder(network)
            {
                StandardTransactionPolicy = new StandardTransactionPolicy(network)
                {
                    CheckFee = false,
                    MinRelayTxFee = null,
                    CheckScriptPubKey = false
                }
            }
            .AddCoins(coins)
            .AddKeys(keystore._Keys.Select(k => k.Item1).ToArray())
            .AddKnownRedeems(keystore._Scripts.ToArray());
            return builder;
        }
    }
}