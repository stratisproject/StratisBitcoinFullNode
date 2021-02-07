using Xunit;

namespace AddressOwnershipTool.Tests
{
    public class BlockExplorerClientTests
    {
        [Fact]
        public void GivenAddressWIthFunds_WhenCheckingIfHasBalance_ThenTrueIsReturned()
        {
            var address = "Sdm4cmKhBtKehvXkpRSVaTX1RAjsE5CVNc";

            var blockExplorerClient = new BlockExplorerClient();

            var result = blockExplorerClient.HasBalance(address);

            Assert.True(result);
        }
    }
}
