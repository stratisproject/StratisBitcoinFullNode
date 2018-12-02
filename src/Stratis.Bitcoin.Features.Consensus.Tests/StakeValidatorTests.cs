using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Moq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Consensus;
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
        private Mock<IConsensus> consensus;

        public StakeValidatorTests() : base(KnownNetworks.StratisRegTest)
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
        public void GetNextTargetRequired_PoW_BlocksExist_PowNoRetargetEnabled_ReturnsFirstBlockHeaderBits()
        {
            var headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5, includePrevBlock: true, network: this.Network);
            IncrementHeaderBits(headers, Target.Difficulty1);

            var stakeBlockStakePos = new BlockStake();
            stakeBlockStakePos.Flags ^= BlockFlag.BLOCK_PROOF_OF_STAKE;
            var stakeBlockStakePow = new BlockStake();

            this.stakeChain.SetupSequence(s => s.Get(It.IsAny<uint256>()))
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePow) // should be returned
                .Returns(stakeBlockStakePow)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos)
                .Returns(stakeBlockStakePos);

            var powLimit = new Target(new uint256("00000000efff0000000000000000000000000000000000000000000000000000"));
            this.consensus.Setup(c => c.PowLimit)
                .Returns(powLimit);

            this.consensus.Setup(c => c.PowNoRetargeting)
                .Returns(true);

            var result = this.stakeValidator.GetNextTargetRequired(this.stakeChain.Object, headers.Last(), this.consensus.Object, false);

            var expectedTarget = headers.Last().Previous.Header.Bits;

            Assert.Equal(expectedTarget, result);
        }

        [Fact]
        public void GetNextTargetRequired_PoS_BlocksExist_PowNoRetargetEnabled_ReturnsFirstBlockHeaderBits()
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

            this.consensus.Setup(c => c.PowNoRetargeting)
                .Returns(true);


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
        public void GetNextTargetRequired_PoS_BlocksExist_PowNoRetargetDisabled_CalculatesRetarget()
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

            this.consensus.Setup(c => c.PowNoRetargeting)
                .Returns(false);

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
        public void CheckProofOfStake_CoinsNotInCoinView_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction();
            Assert.True(transaction.IsCoinStake);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync((FetchCoinsResponse)null);

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_LessThanOneCoinsInCoinView_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction();
            Assert.True(transaction.IsCoinStake);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(new Utilities.UnspentOutputs[0], uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_MoreThanOneCoinsInCoinView_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction();
            Assert.True(transaction.IsCoinStake);

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs(),
                new Utilities.UnspentOutputs(),
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }


        [Fact]
        public void CheckProofOfStake_SingleNullValueCoinInCoinView_ThrowsConsensusError()
        {
            var chainedHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var transaction = CreateStubCoinStakeTransaction();
            Assert.True(transaction.IsCoinStake);

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                null
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));
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

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs()
                {
                    Outputs = new TxOut[] { new TxOut() }
                }
            };

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, uint256.One)); // invalid hash not matching previousTx

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckProofOfStake(new PosRuleContext(), chainedHeader, new BlockStake(), transaction, 15));
            Assert.Equal(ConsensusErrors.CoinstakeVerifySignatureFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckProofOfStake_InvalidSignature_Cont_ThrowsConsensusError()
        {
            Assert.True(false, "add test cases for all invalid signature cases");
        }

        [Fact]
        public void CheckProofOfStake_InvalidStakeDepth_ThrowsConsensusError()
        {
            Assert.True(false, "add test cases for all invalid stake depth cases");
        }

        [Fact]
        public void CheckProofOfStake_CheckKernalHashInvalid_ThrowsConsensusError()
        {
            Assert.True(false, "add test cases for all tests you make for CheckStakeKernelHash");
        }

        [Fact]
        public void ComputeStakeModifierV2_PrevChainedHeaderNull_ReturnsZero()
        {
            var result = this.stakeValidator.ComputeStakeModifierV2(null, null, uint256.One);

            Assert.Equal(uint256.Zero, result);
        }

        [Fact]
        public void ComputeStakeModifierV2_UsingBlockStakeAndKernel_CalculatesStakeModifierHash()
        {
            var blockStake = new BlockStake()
            {
                StakeModifierV2 = 1273671
            };

            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), blockStake, uint256.One);

            Assert.Equal(new uint256("9a37d63a1cddaeb9b018d24f05020d46945b0292f5642cbbcf3b204a14d3748d"), result);
        }

        [Fact]
        public void ComputeStakeModifierV2_UsingChangedBlockStakeAndKernel_CalculatesStakeModifierHash()
        {
            var blockStake = new BlockStake()
            {
                StakeModifierV2 = 12
            };

            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), blockStake, uint256.One);

            Assert.Equal(new uint256("f9a82ef89e0bf841dd9a6b5cea0131a61ea3e2e4a3d1ab56eca5a8ee4da1dade"), result);
        }


        [Fact]
        public void ComputeStakeModifierV2_UsingBlockStakeAndChangedKernel_CalculatesStakeModifierHash()
        {
            var blockStake = new BlockStake()
            {
                StakeModifierV2 = 1273671
            };

            var result = this.stakeValidator.ComputeStakeModifierV2(ChainedHeadersHelper.CreateGenesisChainedHeader(), blockStake, new uint256(2));

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
                .ReturnsAsync(new FetchCoinsResponse(new Utilities.UnspentOutputs[0], uint256.One));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), ChainedHeadersHelper.CreateGenesisChainedHeader(), 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.ReadTxPrevFailed.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_MoreThanOneCoinsInCoinView_ThrowsConsensusError()
        {

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs(),
                new Utilities.UnspentOutputs(),
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

            var unspentoutputs = new Utilities.UnspentOutputs[]
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
            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs()
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

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs((uint)header.Height, header.Block.Transactions[0])
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

            var unspentoutputs = new Utilities.UnspentOutputs[]
            {
                new Utilities.UnspentOutputs((uint)stakableHeader.Height, stakableHeader.Block.Transactions[0])
            };

            this.stakeChain.Setup(s => s.Get(header.HashBlock))
                .Returns((BlockStake)null);

            this.coinView.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), default(CancellationToken)))
                .ReturnsAsync(new FetchCoinsResponse(unspentoutputs, header.HashBlock));

            var exception = Assert.Throws<ConsensusErrorException>(() => this.stakeValidator.CheckKernel(new PosRuleContext(), header, 15, 15, new OutPoint(uint256.One, 12)));
            Assert.Equal(ConsensusErrors.BadStakeBlock.Code, exception.ConsensusError.Code);
        }

        [Fact]
        public void CheckKernel_Cont_ThrowsConsensusError()
        {
            Assert.True(false, "add test cases for all checkkernelhash cases");
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthSmallerThanTargetDepth_ReturnsTrue()
        {
            Assert.True(false, "todo: reverse numbers on genesis and coins");
            var trans = CreateStubCoinStakeTransaction();

            var referenceHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var coins = new Utilities.UnspentOutputs((uint)referenceHeader.Height + 9, trans);
            var targetDepth = referenceHeader.Height + 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.True(result);
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthEqualToTargetDepth_ReturnsFalse()
        {
            Assert.True(false, "todo: reverse numbers on genesis and coins");
            var trans = CreateStubCoinStakeTransaction();

            var referenceHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var coins = new Utilities.UnspentOutputs((uint)referenceHeader.Height + 10, trans);
            var targetDepth = referenceHeader.Height + 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.False(result);
        }

        [Fact]
        public void IsConfirmedInNPrevBlocks_ActualDepthHigherThanTargetDepth_ReturnsFalse()
        {
            Assert.True(false, "todo: reverse numbers on genesis and coins");
            var trans = CreateStubCoinStakeTransaction();

            var referenceHeader = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var coins = new Utilities.UnspentOutputs((uint)referenceHeader.Height + 11, trans);
            var targetDepth = referenceHeader.Height + 10;

            var result = this.stakeValidator.IsConfirmedInNPrevBlocks(coins, referenceHeader, targetDepth);

            Assert.False(result);
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

        private ChainedHeader CreateChainWithStubCoinStakeTransactions(ConcurrentChain chain, int height)
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
                block.Transactions.Add(CreateStubCoinStakeTransaction());

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

        private Transaction CreateStubCoinStakeTransaction()
        {
            Transaction previousTx = this.Network.CreateTransaction();
            previousTx.AddOutput(new TxOut());

            return CreateStubCoinStakeTransaction(previousTx);
        }

        private Transaction CreateStubCoinStakeTransaction(Transaction previousTx)
        {
            Transaction coinstakeTx = this.Network.CreateTransaction();
            coinstakeTx.AddOutput(new TxOut(0, Script.Empty));
            coinstakeTx.AddOutput(new TxOut(50, new Script()));
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

    }
}