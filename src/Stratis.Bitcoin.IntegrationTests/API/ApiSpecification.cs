using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void getgeneralinfo_returns_json_starting_with_wallet_path()
        {
            Given(a_proof_of_stake_node_api);
            And(a_wallet);
            When(getting_general_info);
            Then(data_starting_with_wallet_file_path_is_returned);
        }

        [Fact]
        public void startstaking_enables_staking_but_nothing_staked()
        {
            Given(a_proof_of_stake_node_api);
            And(a_wallet);
            When(staking_is_started);
            Then(staking_is_enabled_but_nothing_is_staked);
        }

        [Fact]
        public void getblockhash_via_rpc_callbyname_returns_the_blockhash()
        {
            Given(a_proof_of_stake_node_api);
            When(calling_rpc_getblockhash_via_callbyname);
            Then(the_blockhash_is_returned);
        }

        [Fact]
        public void listmethods_via_rpc_returns_non_empty_list()
        {
            Given(a_proof_of_stake_node_api);
            When(calling_rpc_listmethods);
            Then(non_empty_list_returned);
        }
    }
}