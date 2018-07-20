using Xunit;
// ReSharper disable once InconsistentNaming
namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class Wallet_address_generation_and_funds_visibility
    {
        [Fact]
        public void Coins_beyond_gap_limit_not_visble_in_balance()
        {
            Given(a_default_gap_limit_of_20);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_zero);
        }

        [Fact]
        public void Coins_are_visible_when_addresses_have_been_requested_prior_to_syncing_blocks()
        {
            Given(a_default_gap_limit_of_20);
            And(_21_new_addresses_are_requested);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_NOT_zero);
        }

        [Fact]
        public void Coins_beyond_default_gap_limit_ARE_visble_in_balance_when_gap_limit_overridden()
        {
            Given(a_gap_limit_of_21);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_NOT_zero);
        }
    }
}