using System;
using System.Linq;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.DataEncoders;
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
            Then(it_is_rejected_and_user_is_told_to_restore_instead);
        }
    }
}