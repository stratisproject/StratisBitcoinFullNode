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
            Given(a_pow_node_with_api_enabled);
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
            Given(sending_node_and_receiving_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);

            When(calling_block_with_valid_hash_via_api_returns_block);

            Then(the_real_block_should_be_retrieved);
            And(the_block_should_contain_the_transaction);
        }

        [Fact]
        public void Getblockcount_via_api_returns_tipheight()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);

            When(calling_getblockcount_via_api_returns_an_int);

            Then(the_blockcount_should_match_consensus_tip_height);
        }

        [Fact]
        public void Getpeerinfo_via_api_returns()
        {
            Given(a_pow_node_with_api_enabled);
        }

        [Fact]
        public void Getbestblockhash_via_api_returns_tip_hash()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);

            When(calling_getbestblockhash_via_api);

            Then(the_consensus_tip_blockhash_is_returned);
        }

        [Fact]
        public void Getblockhash_via_api_returns()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
        }

        [Fact]
        public void Getrawmempool_via_api_returns()
        {
            Given(sending_node_and_receiving_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
        }

        [Fact]
        public void Getblockheader_via_api_returns()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
        }

        [Fact]
        public void Getrawtransaction_nonverbose_via_api_returns()
        {
            Given(sending_node_and_receiving_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
        }

        [Fact]
        public void Getrawtransaction_verbose_via_api_returns()
        {
            Given(sending_node_and_receiving_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
        }
    }
}