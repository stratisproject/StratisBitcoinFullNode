using Xunit;
// ReSharper disable once InconsistentNaming

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class Customisable_address_gap_limit_beyond_BIP44
    {
        [Fact]
        public void Coins_beyond_gap_limit_not_visble_in_balance()
        {
            Given(a_default_gap_limit_of_20);
            And(a_wallet_with_funds_at_index_20);
            When(getting_wallet_balance);
            Then(the_balance_is_zero);

            Given(_21_new_addresses_are_requested);
            When(getting_wallet_balance);
            Then(the_balance_is_no_longer_zero);
        }
    }
}
