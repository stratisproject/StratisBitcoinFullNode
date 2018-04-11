using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class TransactionsTests : BddSpecification
    {
        protected TransactionsTests(ITestOutputHelper output) : base(output)
        {
        }
        protected override void BeforeTest()
        {
            // Start a fullnode
        }

        [Fact]
        public void A_nulldata_transaction_is_sent_to_the_network()
        {
            Given(a_node_running);
            And(a_nulldata_transaction);
            When(the_transaction_is_broadcasted);
            Then(no_error_should_happen);
            And(the_transaction_should_get_confirmed);
            And(the_transaction_should_appear_in_the_blockchain);
        }

        private void the_transaction_should_appear_in_the_blockchain()
        {
            throw new NotImplementedException();
        }

        private void the_transaction_should_get_confirmed()
        {
            throw new NotImplementedException();
        }

        private void no_error_should_happen()
        {
            throw new NotImplementedException();
        }

        private void the_transaction_is_broadcasted()
        {
            throw new NotImplementedException();
        }

        private void a_nulldata_transaction()
        {
            throw new NotImplementedException();
        }

        private void a_node_running()
        {
            throw new NotImplementedException();
        }

        protected override void AfterTest()
        {
            throw new NotImplementedException();
        }
    }
}
