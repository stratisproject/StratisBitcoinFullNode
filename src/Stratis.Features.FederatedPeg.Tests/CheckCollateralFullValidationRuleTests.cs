using System;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Collateral;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CheckCollateralFullValidationRuleTests
    {
        private readonly CheckCollateralFullValidationRule rule;

        private readonly Mock<IInitialBlockDownloadState> ibdMock;

        private readonly Mock<ICollateralChecker> collateralCheckerMock;

        private readonly Mock<ISlotsManager> slotsManagerMock;

        private readonly RuleContext ruleContext;

        public CheckCollateralFullValidationRuleTests()
        {
            this.ibdMock = new Mock<IInitialBlockDownloadState>();
            this.collateralCheckerMock = new Mock<ICollateralChecker>();
            this.slotsManagerMock = new Mock<ISlotsManager>();


            this.ibdMock.Setup(x => x.IsInitialBlockDownload()).Returns(false);
            this.slotsManagerMock
                .Setup(x => x.GetFederationMemberForTimestamp(It.IsAny<uint>(), null))
                .Returns(new CollateralFederationMember(new Key().PubKey, new Money(1), "addr1"));

            this.ruleContext = new RuleContext(new ValidationContext(), DateTimeOffset.Now);
            this.ruleContext.ValidationContext.BlockToValidate = new Block(new BlockHeader() { Time = 5234 });

            Block block = this.ruleContext.ValidationContext.BlockToValidate;
            block.AddTransaction(new Transaction());

            CollateralHeightCommitmentEncoder encoder = new CollateralHeightCommitmentEncoder();

            byte[] encodedHeight = encoder.EncodeWithPrefix(1000);

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(encodedHeight));
            block.Transactions[0].AddOutput(Money.Zero, votingOutputScript);

            this.rule = new CheckCollateralFullValidationRule(this.ibdMock.Object, this.collateralCheckerMock.Object, this.slotsManagerMock.Object, new Mock<IDateTimeProvider>().Object, new PoANetwork());
            this.rule.Logger = new ExtendedLoggerFactory().CreateLogger(this.rule.GetType().FullName);
            this.rule.Initialize();
        }

        [Fact]
        public async Task SkippedIfIBDAsync()
        {
            this.ibdMock.Setup(x => x.IsInitialBlockDownload()).Returns(true);

            await this.rule.RunAsync(new RuleContext(new ValidationContext(), DateTimeOffset.Now));
        }

        [Fact]
        public async Task PassesIfCollateralIsOkAsync()
        {
            this.collateralCheckerMock.Setup(x => x.CheckCollateral(It.IsAny<IFederationMember>(), It.IsAny<int>())).Returns(true);
            this.collateralCheckerMock.Setup(x => x.GetCounterChainConsensusHeight()).Returns(5000);

            await this.rule.RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task ThrowsIfCollateralCheckFailsAsync()
        {
            this.collateralCheckerMock.Setup(x => x.CheckCollateral(It.IsAny<IFederationMember>(), It.IsAny<int>())).Returns(false);

            await Assert.ThrowsAsync<ConsensusErrorException>(() => this.rule.RunAsync(this.ruleContext));
        }
    }
}
