using Stratis.SmartContracts.IntegrationTests.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractInternalTransferTests : IClassFixture<MockChainFixture>
    {
        private readonly Chain mockChain;
        private readonly Node sender;
        private readonly Node receiver;

        public ContractInternalTransferTests(MockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
            this.sender = this.mockChain.Nodes[0];
            this.receiver = this.mockChain.Nodes[1];
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_ToWalletAddress()
        {
            //Transfer to human
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_ToContractAddress()
        {
            //Transfer to contract
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_BetweenContracts()
        {
            //Method calls back and forth between 2 contracts
        }

        [Fact(Skip = "TODO")]
        public void InternalTransfer_FromConstructor()
        {
            //Transfer from constructor
        }

        [Fact]
        public void InternalTransfer_Create_WithValueTransfer()
        {
            //Create with value transfer
        }

    }
}
