using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class RetrieveFromBlockStoreSpecification : BddSpecification
    {
        private readonly SharedSteps sharedSteps;

        private NodeBuilder builder;
        private CoreNode node;
        private List<uint256> blockIds;
        private IList<Block> retrievedBlocks;
        private string password = "P@ssw0rd";
        private WalletAccountReference miningWalletAccountReference;
        private HdAddress minerAddress;
        private Features.Wallet.Wallet miningWallet;
        private Key key;
        private int maturity;
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


        public RetrieveFromBlockStoreSpecification(ITestOutputHelper output) : base(output)
        {
            this.sharedSteps = new SharedSteps();
        }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(caller: this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
        }

        public void a_pow_node_running()
        {
            this.node = this.builder.CreateStratisPowNode();
            this.node.Start();
            this.node.NotInIBD();
        }

        private void a_pow_node_to_transact_with()
        {
            this.transactionNode = this.builder.CreateStratisPowNode();
            this.transactionNode.Start();
            this.transactionNode.NotInIBD();

            this.transactionNode.CreateRPCClient().AddNode(this.node.Endpoint, true);
            this.sharedSteps.WaitForNodeToSync(this.node, this.transactionNode);

            this.transactionNode.FullNode.WalletManager().CreateWallet(this.password, "receiver");
            this.receiverAddress = this.transactionNode.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference("receiver", "account 0"));
        }

        private void a_miner_validating_blocks()
        {
            this.node.FullNode.WalletManager().CreateWallet(this.password, "miner");
            this.miningWalletAccountReference = new WalletAccountReference("miner", "account 0");
            this.minerAddress = this.node.FullNode.WalletManager().GetUnusedAddress(this.miningWalletAccountReference);
            this.miningWallet = this.node.FullNode.WalletManager().GetWalletByName("miner");

            this.key = this.miningWallet.GetExtendedPrivateKeyForAddress(this.password, this.minerAddress).PrivateKey;
            this.node.SetDummyMinerSecret(new BitcoinSecret(this.key, this.node.FullNode.Network));
        }

        public void some_real_blocks_with_a_uint256_identifier()
        {
            this.maturity = (int)this.node.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            this.blockIds = this.node.GenerateStratisWithMiner(this.maturity + 1);
        }

        public void some_blocks_creating_reward()
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

        public void the_node_is_synced()
        {
            this.sharedSteps.WaitForNodeToSync(this.node);
        }

        public void the_nodes_are_synced()
        {
            this.sharedSteps.WaitForNodeToSync(this.node, this.transactionNode);
        }

        public void a_real_transaction()
        {
            var transactionBuildContext = new TransactionBuildContext(
                    this.miningWalletAccountReference,
                    new List<Recipient>() { new Recipient() { Amount = this.transferAmount, ScriptPubKey = this.receiverAddress.ScriptPubKey } },
                    this.password)
            { MinConfirmations = 2 };
            this.transaction = this.node.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);

            this.node.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.blockWithTransactionId = this.node.GenerateStratisWithMiner(1).Single();
            this.node.GenerateStratisWithMiner(1);
            this.sharedSteps.WaitForNodeToSync(this.node, this.transactionNode);
        }

        public async Task trying_to_retrieve_the_blocks_from_the_blockstore()
        {
            this.retrievedBlocks = this.blockIds.Concat(new[] { this.wrongBlockId })
                .Select(async id =>
                    await this.node.FullNode.BlockStoreManager().BlockRepository
                        .GetAsync(id)).Select(b => b.Result).ToList();

            this.retrievedBlocks.Count(b => b != null).Should().Be(this.blockIds.Count);
            this.retrievedBlocks.Count(b => b == null).Should().Be(1);
            this.retrievedBlockHashes = this.retrievedBlocks.Where(b => b != null).Select(b => b.GetHash());

            this.retrievedBlockHashes.Should().OnlyHaveUniqueItems();
        }

        public async Task trying_to_retrieve_the_transactions_by_Id_from_the_blockstore()
        {
            this.retrievedTransaction = await this.node.FullNode.BlockStoreManager().BlockRepository
                .GetTrxAsync(this.transaction.GetHash());
            this.wontRetrieveTransaction = await this.node.FullNode.BlockStoreManager().BlockRepository
                .GetTrxAsync(this.wrongTransactionId);
        }

        public async Task trying_to_retrieve_the_block_containing_the_transactions_from_the_blockstore()
        {
            this.retrievedBlockId = await this.node.FullNode.BlockStoreManager().BlockRepository
                .GetTrxBlockIdAsync(this.transaction.GetHash());
            this.wontRetrieveBlockId = await this.node.FullNode.BlockStoreManager().BlockRepository
                .GetTrxAsync(this.wrongTransactionId);
        }

        public void real_blocks_should_be_retrieved()
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