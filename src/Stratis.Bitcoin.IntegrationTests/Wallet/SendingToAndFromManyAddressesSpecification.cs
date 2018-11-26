using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingToAndFromManyAddressesSpecification
    {
        [Fact]
        public void sending_from_one_address_to_fifty_addresses()
        {
            Given(two_connected_nodes);
            When(node1_sends_funds_to_node2_TO_fifty_addresses);
            Then(node2_receives_the_funds);
        }

        [Fact]
        public void sending_from_fifty_addresses_to_one_address()
        {
            Given(funds_across_fifty_addresses_on_node2_wallet);
            When(node2_sends_funds_to_node1_FROM_fifty_addresses);
            Then(node1_receives_the_funds);
        }
    }
}
