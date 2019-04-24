using Moq;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CheckCollateralFullValidationRuleTests
    {
        private readonly CheckCollateralFullValidationRule rule;

        private readonly Mock<IInitialBlockDownloadState> ibdMock;

        private readonly Mock<ICollateralChecker> collateralCheckerMock;

        private readonly Mock<ISlotsManager> slotsManagerMock;

        public CheckCollateralFullValidationRuleTests()
        {
            this.ibdMock = new Mock<IInitialBlockDownloadState>();
            this.collateralCheckerMock = new Mock<ICollateralChecker>();
            this.slotsManagerMock = new Mock<ISlotsManager>();

            this.rule = new CheckCollateralFullValidationRule(this.ibdMock.Object, this.collateralCheckerMock.Object, this.slotsManagerMock.Object);
        }

        // TODO add test
    }
}
