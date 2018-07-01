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

        [Fact]
        public void Block_with_valid_hash_via_api_returns_transaction_block()
        {
            Given(sending_and_receiving_pos_nodes_with_api_enabled);
            And(genesis_is_mined);
            And(coins_are_mined_to_maturity);
            And(coins_are_mined_past_maturity);
            And(the_pos_node_starts_staking);
            And(some_blocks_creating_reward);
            And(the_nodes_are_synced);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);

            When(calling_block_with_valid_hash_via_api_returns_block);

            Then(the_real_block_should_be_retrieved);
            And(the_block_should_contain_the_transaction);
        }

        [Fact]
        public void GetblockCount_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Addnode_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getpeerinfo_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getbestblockhash_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getblockhash_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getrawmembpool_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getblockheader_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getrawtransaction_nonverbose_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }

        [Fact]
        public void Getrawtransaction_verbose_via_api_returns()
        {
            Given(a_proof_of_stake_node_with_api_enabled);

        }
    }
}