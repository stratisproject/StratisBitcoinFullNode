using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class RetrieveFromBlockStoreSpecification : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode node;
        private List<uint256> blockIds;
        private IList<Block> retrievedBlocks;
        private const string password = "password";
        private const string walletName = "mywallet";
        private WalletAccountReference miningWalletAccountReference;
        private uint256 wrongBlockId;
        private IEnumerable<uint256> retrievedBlockHashes;
        private CoreNode transactionNode;
        private readonly Money transferAmount = Money.COIN * 2;
        private Transaction transaction;
        private HdAddress receiverAddress;
        private uint256 blockWithTransactionId;
        private Transaction retrievedTransaction;
        private uint256 wrongTransactionId;
        private Transaction wontRetrieveTransaction;
        private uint256 retrievedBlockId;
        private Transaction wontRetrieveBlockId;
        private readonly Network network = new BitcoinRegTest();

        public RetrieveFromBlockStoreSpecification(ITestOutputHelper output) : base(output) { }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
        }

        private void a_pow_node_running()
        {
            this.node = this.builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(Common.ReadyData.ReadyBlockchain.BitcoinRegTest100Miner).Start();
        }

        private void a_pow_node_to_transact_with()
        {
            this.transactionNode = this.builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(Common.ReadyData.ReadyBlockchain.BitcoinRegTest100Miner).Start();
            TestHelper.Connect(this.transactionNode, this.node);
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);

            this.receiverAddress = this.transactionNode.FullNode.WalletManager().GetUnusedAddress();
        }

        private void a_miner_validating_blocks()
        {
            this.miningWalletAccountReference = new WalletAccountReference(walletName, "account 0");
        }

        private void some_real_blocks_with_a_uint256_identifier()
        {
            this.blockIds = TestHelper.MineBlocks(this.node, 1).BlockHashes;
        }

        private void some_blocks_creating_reward()
        {
            this.some_real_blocks_with_a_uint256_identifier();
        }

        private void a_wrong_block_id()
        {
            this.wrongBlockId = new uint256(3141592653589793238);
            this.blockIds.Should().NotContain(this.wrongBlockId, "it would corrupt the test");
        }

        private void a_wrong_transaction_id()
        {
            this.wrongTransactionId = new uint256(314159265358979323);
            this.transaction.GetHash().Should().NotBe(this.wrongTransactionId, "it would corrupt the test");
        }

        private void the_node_is_synced()
        {
            TestHelper.WaitForNodeToSync(this.node);
        }

        private void the_nodes_are_synced()
        {
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);
        }

        private void a_real_transaction()
        {
            var transactionBuildContext = new TransactionBuildContext(this.node.FullNode.Network)
            {
                AccountReference = this.miningWalletAccountReference,
                MinConfirmations = (int)this.node.FullNode.Network.Consensus.CoinbaseMaturity,
                WalletPassword = password,
                Recipients = new List<Recipient>() { new Recipient() { Amount = this.transferAmount, ScriptPubKey = this.receiverAddress.ScriptPubKey } }
            };

            this.transaction = this.node.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);

            this.node.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.blockWithTransactionId = TestHelper.MineBlocks(this.node, 2).BlockHashes[0];
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);
        }

        private void trying_to_retrieve_the_blocks_from_the_blockstore()
        {
            this.retrievedBlocks = this.blockIds.Concat(new[] { this.wrongBlockId })
                .Select(id => this.node.FullNode.BlockStore().GetBlockAsync(id).GetAwaiter().GetResult()).Select(b => b).ToList();

            this.retrievedBlocks.Count(b => b != null).Should().Be(this.blockIds.Count);
            this.retrievedBlocks.Count(b => b == null).Should().Be(1);
            this.retrievedBlockHashes = this.retrievedBlocks.Where(b => b != null).Select(b => b.GetHash());

            this.retrievedBlockHashes.Should().OnlyHaveUniqueItems();
        }

        private void trying_to_retrieve_the_transactions_by_Id_from_the_blockstore()
        {
            this.retrievedTransaction = this.node.FullNode.BlockStore().GetTransactionByIdAsync(this.transaction.GetHash()).GetAwaiter().GetResult();
            this.wontRetrieveTransaction = this.node.FullNode.BlockStore().GetTransactionByIdAsync(this.wrongTransactionId).GetAwaiter().GetResult();
        }

        private void trying_to_retrieve_the_block_containing_the_transactions_from_the_blockstore()
        {
            this.retrievedBlockId = this.node.FullNode.BlockStore()
                .GetBlockIdByTransactionIdAsync(this.transaction.GetHash()).GetAwaiter().GetResult();
            this.wontRetrieveBlockId = this.node.FullNode.BlockStore()
                .GetTransactionByIdAsync(this.wrongTransactionId).GetAwaiter().GetResult();
        }

        private void real_blocks_should_be_retrieved()
        {
            this.retrievedBlockHashes.Should().BeEquivalentTo(this.blockIds);
        }

        private void the_wrong_block_id_should_return_null()
        {
            this.retrievedBlockHashes.Should().NotContain(this.wrongBlockId);
        }

        private void the_real_transaction_should_be_retrieved()
        {
            this.retrievedTransaction.Should().NotBeNull();
            this.retrievedTransaction.GetHash().Should().Be(this.transaction.GetHash());
            this.retrievedTransaction.Outputs.Should()
                .Contain(t => t.Value == this.transferAmount.Satoshi
                              && t.ScriptPubKey.GetDestinationAddress(this.node.FullNode.Network).ScriptPubKey == this.receiverAddress.ScriptPubKey);
        }

        private void the_wrong_transaction_id_should_return_null()
        {
            this.wontRetrieveTransaction.Should().BeNull();
        }

        private void the_block_with_the_real_transaction_should_be_retrieved()
        {
            this.retrievedBlockId.Should().NotBeNull();
            this.retrievedBlockId.Should().Be(this.blockWithTransactionId);
        }

        private void the_block_with_the_wrong_id_should_return_null()
        {
            this.wontRetrieveBlockId.Should().BeNull();
        }
    }
}