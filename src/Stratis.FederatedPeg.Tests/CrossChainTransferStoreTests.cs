using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class CrossChainTransferStoreTests : CrossChainTestBase
    {
        public CrossChainTransferStoreTests() : base()
        {
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWallet()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(5);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWalletAndSurvivesRestart()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(5);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                this.federationWalletManager.SaveWallet();

                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore.TipHashAndHeight.Height);
            }

            // Create a new instance of this test that loads from the persistence that we created in the step before.
            var newTest = new CrossChainTransferStoreTests();

            // Force a reorg by creating a new chain that only has genesis in common.
            newTest.Init(dataFolder);
            newTest.AppendBlocks(3);

            using (ICrossChainTransferStore crossChainTransferStore2 = newTest.CreateStore())
            {
                // Test that synchronizing the store aligns it with the current chain tip after the fork.
                crossChainTransferStore2.Initialize();
                crossChainTransferStore2.Start();

                Assert.Equal(newTest.wallet.LastBlockSyncedHash, crossChainTransferStore2.TipHashAndHeight.HashBlock);
                Assert.Equal(newTest.wallet.LastBlockSyncedHeight, crossChainTransferStore2.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Recording deposits when the wallet UTXOs are sufficient succeeds with deterministic transactions.
        /// </summary>
        [Fact]
        public void StoringDepositsWhenWalletBalanceSufficientSucceedsWithDeterministicTransactions()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                Deposit deposit2 = new Deposit(1, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                IMaturedBlockDeposits[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                Transaction[] transactions = crossChainTransferStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.Partial).GetAwaiter().GetResult().Values.ToArray();

                Assert.Equal(2, transactions.Length);

                // Transactions[0] inputs.
                Assert.Equal(2, transactions[0].Inputs.Count);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[0].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)1, transactions[0].Inputs[1].PrevOut.N);

                // Transaction[0] outputs.
                Assert.Equal(3, transactions[0].Outputs.Count);

                // Transaction[0] output value - change.
                Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transactions[0].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[0].Outputs[0].ScriptPubKey);

                // Transaction[0] output value - recipient 1.
                Assert.Equal(new Money(160m, MoneyUnit.BTC), transactions[0].Outputs[1].Value);
                Assert.Equal(address1.ScriptPubKey, transactions[0].Outputs[1].ScriptPubKey);

                // Transaction[0] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[0].Outputs[2].Value);
                Assert.Equal(deposit1.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[0]));

                // Transactions[1] inputs.
                Assert.Single(transactions[1].Inputs);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change.
                Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2.
                Assert.Equal(new Money(60m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[1].Outputs[2].Value);
                Assert.Equal(deposit2.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1]));

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { 0, 1 }).GetAwaiter().GetResult().ToArray();

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(deposit1.Amount, new Money(transfers[0].DepositAmount));
                Assert.Equal(address1.ScriptPubKey, transfers[0].DepositTargetAddress);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(deposit2.Amount, new Money(transfers[1].DepositAmount));
                Assert.Equal(address2.ScriptPubKey, transfers[1].DepositTargetAddress);
            }
        }

        /// <summary>
        /// Recording deposits when the wallet UTXOs are sufficient succeeds with deterministic transactions.
        /// </summary>
        [Fact]
        public void StoringDepositsWhenWalletBalanceInSufficientSucceedsWithSuspendStatus()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                Deposit deposit2 = new Deposit(1, new Money(100m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                IMaturedBlockDeposits[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { 0, 1 }).GetAwaiter().GetResult().ToArray();

                Transaction[] transactions = transfers.Select(t => t.PartialTransaction).ToArray();

                Assert.Equal(2, transactions.Length);

                // Transactions[0] inputs.
                Assert.Equal(2, transactions[0].Inputs.Count);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[0].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)1, transactions[0].Inputs[1].PrevOut.N);

                // Transaction[0] outputs.
                Assert.Equal(3, transactions[0].Outputs.Count);

                // Transaction[0] output value - change.
                Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transactions[0].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[0].Outputs[0].ScriptPubKey);

                // Transaction[0] output value - recipient 1.
                Assert.Equal(new Money(160m, MoneyUnit.BTC), transactions[0].Outputs[1].Value);
                Assert.Equal(address1.ScriptPubKey, transactions[0].Outputs[1].ScriptPubKey);

                // Transaction[0] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[0].Outputs[2].Value);
                Assert.Equal(deposit1.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[0]));

                Assert.Null(transactions[1]);

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(deposit1.Amount, new Money(transfers[0].DepositAmount));
                Assert.Equal(address1.ScriptPubKey, transfers[0].DepositTargetAddress);
                Assert.Equal(CrossChainTransferStatus.Suspended, transfers[1].Status);

                // Add more funds and resubmit the deposits.
                AddFundingTransaction(new Money[] { Money.COIN * 1000 });
                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();
                transfers = crossChainTransferStore.GetAsync(new uint256[] { 0, 1 }).GetAwaiter().GetResult().ToArray();
                transactions = transfers.Select(t => t.PartialTransaction).ToArray();

                // Transactions[1] inputs.
                Assert.Equal(2, transactions[1].Inputs.Count);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change.
                Assert.Equal(new Money(969.99m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2.
                Assert.Equal(new Money(100m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(0m, MoneyUnit.BTC), transactions[1].Outputs[2].Value);
                Assert.Equal(deposit2.Id.ToString(), new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1]));

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(deposit2.Amount, new Money(transfers[1].DepositAmount));
                Assert.Equal(address2.ScriptPubKey, transfers[1].DepositTargetAddress);

                (Money confirmed, Money unconfirmed) spendable = this.wallet.GetSpendableAmount();

                Assert.Equal(new Money(979.98m, MoneyUnit.BTC), spendable.unconfirmed);
            }
        }

        /// <summary>
        /// Tests whether the store merges signatures as expected.
        /// </summary>
        [Fact]
        public void StoreMergesSignaturesAsExpected()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                IMaturedBlockDeposits[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                ICrossChainTransfer crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction transaction = crossChainTransfer.PartialTransaction;

                Assert.True(CrossChainTransferStore.ValidateTransaction(transaction, this.wallet));

                // Create a separate instance to generate another transaction.
                Transaction transaction2;
                var newTest = new CrossChainTransferStoreTests();
                DataFolder dataFolder2 = new DataFolder(CreateTestDir(this));

                newTest.federationKeys = this.federationKeys;
                newTest.SetExtendedKey(1);
                newTest.Init(dataFolder2);
                newTest.AddFunding();
                newTest.AppendBlocks(3);

                using (ICrossChainTransferStore crossChainTransferStore2 = newTest.CreateStore())
                {
                    crossChainTransferStore2.Initialize();
                    crossChainTransferStore2.Start();

                    Assert.Equal(newTest.chain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.HashBlock);
                    Assert.Equal(newTest.chain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);

                    crossChainTransferStore2.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                    ICrossChainTransfer crossChainTransfer2 = crossChainTransferStore2.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                    Assert.NotNull(crossChainTransfer2);

                    transaction2 = crossChainTransfer2.PartialTransaction;

                    Assert.True(CrossChainTransferStore.ValidateTransaction(transaction2, newTest.wallet));
                }

                // Merges the transaction signatures.
                crossChainTransferStore.MergeTransactionSignaturesAsync(deposit.Id, new[] { transaction2 }).GetAwaiter().GetResult();

                // Test the outcome.
                crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);
                Assert.Equal(CrossChainTransferStatus.FullySigned, crossChainTransfer.Status);

                // Should be returned as signed.
                Transaction signedTransaction = crossChainTransferStore.GetTransactionsByStatusAsync(CrossChainTransferStatus.FullySigned).GetAwaiter().GetResult().Values.SingleOrDefault();
                Assert.NotNull(signedTransaction);

                // Check ths signature.
                Assert.True(CrossChainTransferStore.ValidateTransaction(signedTransaction, this.wallet, true));
            }
        }

        /// <summary>
        /// Tests that the store can recover from the chain and handle a re-org.
        /// </summary>
        [Fact]
        public void StoreRecoversFromChain()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(3);

            Transaction transaction = this.network.CreateTransaction("0100000002d2eefdc21fb0598664f8fcc8e7866969b9d7bb53f9f8dfb7823ee3eeadb33cbf00000000fd410100473044022038e16c1d58982bf964c186e4d72d372d16a3a63883fa7608261cdb0fdef519e50220149791d4956f0d6ddd61f754274513b2d5ad5fc1df1bed02b1f0f1c7df5322cd01483045022100eb553d5f199cd0aad40b7dd5773741f136e23cf681cc2fb7c2d66a07c2110c3d02207ccbe4547b92ba0ea2e1a01811325ee406c11e5bc832df9c40df9e0bfac59d0a014cad52210332195438ff52eb495321ffb598054e95c8b9aced0238ecf389a55e87f9d26c732103b1af6922b8fc1cc05bec47fdb54f2f85d00a3b4152c7d4bdb5a1e62fea558a362102c426f49fbf65601d7e59bf39cb69a1d488c098aa36caf99cd6ec236830279cdc2102b6128cc9fd484cb2024693e2afbcabecabf44faaa10306459c51a70bb8f510f62103b40dd94129f50521d38b7014295a5c48f2be01eceef2336cc44f321cc613a0c655aeffffffffd2eefdc21fb0598664f8fcc8e7866969b9d7bb53f9f8dfb7823ee3eeadb33cbf01000000fd420100483045022100e232b626ca289b6fe76b4ed011430d67a0243f5a135d5342ea3cd6b02f6bdb780220679de7791bba313373ea906d4236b48358fb00934ac658ad889a7f0e5654c4a301483045022100b698390a1d0c67495be0e14f56e75520e39fa6b01e60e82cc19be32b63b8cc0602206f146ce0324ab795faf19e0802d03c8f036c5cf7ce25d8a70b9b3f17438c2fbf014cad52210332195438ff52eb495321ffb598054e95c8b9aced0238ecf389a55e87f9d26c732103b1af6922b8fc1cc05bec47fdb54f2f85d00a3b4152c7d4bdb5a1e62fea558a362102c426f49fbf65601d7e59bf39cb69a1d488c098aa36caf99cd6ec236830279cdc2102b6128cc9fd484cb2024693e2afbcabecabf44faaa10306459c51a70bb8f510f62103b40dd94129f50521d38b7014295a5c48f2be01eceef2336cc44f321cc613a0c655aeffffffff03c0878b3b0000000017a914623b4eab628d77b96c41df080122ac2c037954138700a0acb9030000001976a914b79f48d2e34702cf134bfbbd3f7787c1db4acd3688ac0000000000000000226a20000000000000000000000000000000000000000000000000000000000000000000000000");
            this.AppendBlock(transaction);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                ICrossChainTransfer transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(transfer);
                Assert.Equal(transaction.GetHash(), transfer.PartialTransaction.GetHash());
                Assert.Equal(CrossChainTransferStatus.SeenInBlock, transfer.Status);

                // Re-org the chain.
                this.chain.SetTip(this.chain.Tip.Previous);
                this.federationWalletManager.UpdateLastBlockSyncedHeight(this.chain.Tip);

                transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                // Since the info from chain A has not been recovered yet we expect that
                // that the transfer is completely removed from the DB - I.e. it has only been "seen"
                // and had no associated deposit height.
                Assert.Null(transfer);

                // Restore the chain.
                AppendBlock(transaction);
                this.federationWalletManager.UpdateLastBlockSyncedHeight(this.chain.Tip);

                transfer = crossChainTransferStore.GetAsync(new uint256[] { 0 }).GetAwaiter().GetResult().SingleOrDefault();

                // Check that the status reverts for a transaction that is again visible on the chain.
                Assert.Equal(CrossChainTransferStatus.SeenInBlock, transfer.Status);
            }
        }

        /// <summary>
        /// Check that partial transactions present in the store cause partial transaction requests made to peers.
        /// </summary>
        [Fact]
        public void StoredPartialTransactionsTriggerSignatureRequests()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(5);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                Deposit deposit2 = new Deposit(1, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                IMaturedBlockDeposits[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                Dictionary<uint256, Transaction> transactions = crossChainTransferStore.GetTransactionsByStatusAsync(
                    CrossChainTransferStatus.Partial).GetAwaiter().GetResult();

                PartialTransactionRequester requester = new PartialTransactionRequester(this.loggerFactory, crossChainTransferStore, this.asyncLoopFactory,
                    this.nodeLifetime, this.connectionManager, this.federationGatewaySettings);

                System.Net.IPEndPoint peerEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("1.2.3.4"), 5);
                INetworkPeer peer = Substitute.For<INetworkPeer>();
                peer.RemoteSocketAddress.Returns(peerEndPoint.Address);
                peer.RemoteSocketPort.Returns(peerEndPoint.Port);
                peer.PeerEndPoint.Returns(peerEndPoint);

                var peers = new NetworkPeerCollection();
                peers.Add(peer);

                this.federationGatewaySettings.FederationNodeIpEndPoints.Returns(new[] { peerEndPoint });

                this.connectionManager.ConnectedPeers.Returns(peers);

                requester.Start();

                Thread.Sleep(100);

                peer.Received().SendMessageAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                    o.DepositId == 0 && o.PartialTransaction.GetHash() == transactions[0].GetHash())).GetAwaiter().GetResult();

                peer.Received().SendMessageAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                    o.DepositId == 1 && o.PartialTransaction.GetHash() == transactions[1].GetHash())).GetAwaiter().GetResult();
            }
        }
    }
}
