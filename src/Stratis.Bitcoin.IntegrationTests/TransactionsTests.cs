using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class TransactionsTests : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode node;
        private Wallet wallet;

        protected TransactionsTests(ITestOutputHelper output) : base(output)
        {
        }
        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.builder.Dispose();
        }

        [Fact]
        public void A_nulldata_transaction_is_sent_to_the_network()
        {
            Given(a_proof_of_stake_node_running);
            And(a_funded_wallet);
            And(a_nulldata_transaction);
            When(the_transaction_is_broadcasted);
            And(the_block_is_mined);
            Then(no_error_should_happen);
            And(the_transaction_should_get_confirmed);
            And(the_transaction_should_appear_in_the_blockchain);
        }

        #region steps
        private void a_proof_of_stake_node_running()
        {
            this.node = this.builder.CreateStratisPosNode();
            this.node.Start();
            this.node.NotInIBD();
        }

        private void a_funded_wallet()
        {
            this.node.FullNode.WalletManager().CreateWallet("123456", "mywallet");

            HdAddress addr = this.node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            this.wallet = this.node.FullNode.WalletManager().GetWalletByName("mywallet");
            //Key key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;
        }

        private void a_nulldata_transaction()
        {
            wallet.
            node.FullNode.WalletTransactionHandler().BuildTransaction()
        }

        private void the_transaction_is_broadcasted()
        {
            throw new NotImplementedException();
        }
        private void the_block_is_mined()
        {
            throw new NotImplementedException();
        }
        private void no_error_should_happen()
        {
            throw new NotImplementedException();
        }

        private void the_transaction_should_get_confirmed()
        {
            throw new NotImplementedException();
        }

        private void the_transaction_should_appear_in_the_blockchain()
        {
            throw new NotImplementedException();
        } 
        #endregion

    }
}
