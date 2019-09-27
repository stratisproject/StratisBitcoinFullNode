using Xunit;
// ReSharper disable once InconsistentNaming
namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletAddressBuffer
    {
        [Fact]
        public void CoinsBeyondGapLimitAreNotVisible()
        {
            Given(a_default_gap_limit_of_20);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_zero);
        }

        [Fact]
        public void CoinsBeyondDefaultLimitAreVisibleWhenAddressesRequestedBeforeSyncing()
        {
            Given(a_default_gap_limit_of_20);
            And(_21_new_addresses_are_requested);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_NOT_zero);
        }

        [Fact]
        public void CoinsBeyondDefaultGapLimitAREvisbleWhenGapLimitOverridden()
        {
            Given(a_gap_limit_of_21);
            And(a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit);
            When(getting_wallet_balance);
            Then(the_balance_is_NOT_zero);
        }
    }
}