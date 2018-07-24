using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void Getgeneralinfo_returns_json_starting_with_wallet_path()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(calling_general_info_via_api);
            Then(general_information_about_the_wallet_and_node_is_returned);
        }

        [Fact]
        public void Startstaking_enables_staking_but_nothing_staked()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(calling_startstaking_via_api);
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
        public void CreateExtPubKeyOnlyWallet_creates_wallet_with_extra_flag()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            When(calling_recover_via_extpubkey_for_account_0);
            Then(a_wallet_is_created_without_private_key_for_account_0);

            When(calling_recover_via_extpubkey_for_account_1);
            Then(a_wallet_is_created_without_private_key_for_account_1);
        }

        [Fact]
        public void AddNewAccount_for_xpub_only_wallet_informs_user_to_create_new_wallet_()
        {
            Given(a_proof_of_stake_node_with_api_enabled);
            Given(an_extpubkey_only_wallet_with_account_0);
            When(attempting_to_add_an_account);
            Then(it_is_rejected_as_forbidden);
        }

        [Fact]
        public void Block_with_valid_hash_via_api_returns_transaction_block()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
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
        public void Getpeerinfo_via_api_returns_connected_peer()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
            When(calling_getpeerinfo_via_api);
            Then(a_single_connected_peer_is_returned);
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
        public void Getblockhash_via_api_returns_blockhash_at_given_height()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            When(calling_getblockhash_via_api);
            Then(the_blockhash_is_returned);
        }

        [Fact]
        public void Getrawmempool_via_api_finds_mempool_transaction()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            When(calling_getrawmempool_via_api);
            Then(the_transaction_is_found_in_mempool);
        }

        [Fact]
        public void Getblockheader_via_api_returns_blockheader()
        {
            Given(a_pow_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            When(calling_getblockheader_via_api);
            Then(the_blockheader_is_returned);
        }

        [Fact]
        public void Getrawtransaction_nonverbose_via_api_returns_transaction_hash()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_getrawtransaction_nonverbose_via_api);
            Then(the_transaction_hash_is_returned);
        }

        [Fact]
        public void Getrawtransaction_verbose_via_api_returns()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_getrawtransaction_verbose_via_api);
            Then(a_verbose_raw_transaction_is_returned);
        }

        [Fact]
        public void Gettxout_nomempool_via_api_returns_txouts()
        {
            Given(two_connected_pow_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_gettxout_notmempool_via_api);
            Then(the_txout_is_returned);
        }

        [Fact]
        public void Validateaddress_via_api_confirms_valid_address()
        {
            Given(a_pow_node_with_api_enabled);
            When(calling_validateaddress_via_api);
            Then(a_valid_address_is_validated);
        }

        [Fact]
        public void Status_via_api_returns_status_info()
        {
            Given(a_pow_node_with_api_enabled);
            When(calling_status_via_api);
            Then(status_information_is_returned);
        }
    }
}