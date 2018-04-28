using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void Getgeneralinfo_returns_json_starting_with_wallet_path()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(getting_general_info);
            Then(general_information_about_the_wallet_and_node_is_returned);
        }

        [Fact]
        public void Startstaking_enables_staking_but_nothing_staked()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(staking_is_started);
            Then(staking_is_enabled_but_nothing_is_staked);
        }

        [Fact]
        public void Getblockhash_via_rpc_callbyname_returns_the_blockhash()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(calling_rpc_getblockhash_via_callbyname);
            Then(the_blockhash_is_returned);
        }

        [Fact]
        public void Listmethods_via_rpc_returns_non_empty_list()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(calling_rpc_listmethods);
            Then(a_full_list_of_available_commands_is_returned);
        }
    }
}