using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NBitcoin;
using Newtonsoft.Json;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CrossChainTransferStoreStratisTests : CrossChainTransferStoreTests
    {
        public CrossChainTransferStoreStratisTests() : base(new StratisRegTest())
        {
        }
    }

    public class CrossChainTransferStoreTests : CrossChainTestBase
    {
        public CrossChainTransferStoreTests(Network network = null) : base(network)
        {
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact(Skip = TestingValues.SkipTests)]
        public void StartSynchronizesWithWallet()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

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
        [Fact(Skip = TestingValues.SkipTests)]
        public void StartSynchronizesWithWalletAndSurvivesRestart()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                this.federationWalletManager.SaveWallet();

                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.wallet.LastBlockSyncedHeight, crossChainTransferStore.TipHashAndHeight.Height);
            }

            // Create a new instance of this test that loads from the persistence that we created in the step before.
            var newTest = new CrossChainTransferStoreTests(this.network);

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
        [Fact(Skip = TestingValues.SkipTests)]
        public void StoringDepositsWhenWalletBalanceSufficientSucceedsWithDeterministicTransactions()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                var deposit2 = new Deposit(1, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
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
                new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[0], out string actualDepositId);
                Assert.Equal(deposit1.Id.ToString(), actualDepositId);

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
                new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1], out string actualDepositId2);
                Assert.Equal(deposit2.Id.ToString(), actualDepositId2);

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
        [Fact(Skip = TestingValues.SkipTests)]
        public void StoringDepositsWhenWalletBalanceInSufficientSucceedsWithSuspendStatus()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                var deposit2 = new Deposit(1, new Money(100m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
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
                new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[0], out string actualDepositId);
                Assert.Equal(deposit1.Id.ToString(), actualDepositId);

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
                new OpReturnDataReader(this.loggerFactory, this.network).TryGetTransactionId(transactions[1], out string actualDepositId2);
                Assert.Equal(deposit2.Id.ToString(), actualDepositId2);

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
        [Fact(Skip = TestingValues.SkipTests)]
        public void StoreMergesSignaturesAsExpected()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                ICrossChainTransfer crossChainTransfer = crossChainTransferStore.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction transaction = crossChainTransfer.PartialTransaction;

                Assert.True(crossChainTransferStore.ValidateTransaction(transaction));

                // Create a separate instance to generate another transaction.
                Transaction transaction2;
                var newTest = new CrossChainTransferStoreTests(this.network);
                var dataFolder2 = new DataFolder(CreateTestDir(this));

                newTest.federationKeys = this.federationKeys;
                newTest.SetExtendedKey(1);
                newTest.Init(dataFolder2);

                // Clone chain
                for (int i = 1; i <= this.chain.Height; i++)
                {
                    ChainedHeader header = this.chain.GetBlock(i);
                    Block block = this.blockDict[header.HashBlock];
                    newTest.AppendBlock(block);
                }

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

                    Assert.True(crossChainTransferStore2.ValidateTransaction(transaction2));
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
                Assert.True(crossChainTransferStore.ValidateTransaction(signedTransaction, true));
            }
        }

        /// <summary>
        /// Check that partial transactions present in the store cause partial transaction requests made to peers.
        /// </summary>
        [Fact(Skip = TestingValues.SkipTests)]
        public void StoredPartialTransactionsTriggerSignatureRequest()
        {
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(this.chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);
                Assert.Equal(this.chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                var deposit2 = new Deposit(1, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits).GetAwaiter().GetResult();

                Dictionary<uint256, Transaction> transactions = crossChainTransferStore.GetTransactionsByStatusAsync(
                    CrossChainTransferStatus.Partial).GetAwaiter().GetResult();

                var requester = new PartialTransactionRequester(this.loggerFactory, crossChainTransferStore, this.asyncLoopFactory,
                    this.nodeLifetime, this.connectionManager, this.federationGatewaySettings);

                var peerEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("1.2.3.4"), 5);
                var peer = Substitute.For<INetworkPeer>();
                peer.RemoteSocketAddress.Returns(peerEndPoint.Address);
                peer.RemoteSocketPort.Returns(peerEndPoint.Port);
                peer.PeerEndPoint.Returns(peerEndPoint);
                peer.IsConnected.Returns(true);

                var peers = new NetworkPeerCollection();
                peers.Add(peer);

                this.federationGatewaySettings.FederationNodeIpEndPoints.Returns(new[] { peerEndPoint });

                this.connectionManager.ConnectedPeers.Returns(peers);

                requester.Start();

                Thread.Sleep(100);

                peer.Received().SendMessageAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                    o.DepositId == 0 && o.PartialTransaction.GetHash() == transactions[0].GetHash())).GetAwaiter().GetResult();

                peer.DidNotReceive().SendMessageAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                    o.DepositId == 1 && o.PartialTransaction.GetHash() == transactions[1].GetHash())).GetAwaiter().GetResult();
            }
        }

        [Fact]
        public void NextMatureDepositStartsHigherOnMain()
        {
            // This should really be 2 tests in separate classes but we'll fit in with what is already happening for now.

            // Start querying counter-chain for deposits from first non-genesis block on main chain and a higher number on side chain.
            int depositHeight = (this.network.Name == new StratisRegTest().Name)
                ? 1
                : FederationGatewaySettings.StratisMainDepositStartBlock;

            this.federationGatewaySettings.CounterChainDepositStartBlock.Returns(depositHeight);

            var dataFolder = new DataFolder(CreateTestDir(this));

            this.Init(dataFolder);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();

                Assert.Equal(depositHeight, crossChainTransferStore.NextMatureDepositHeight);
            }
        }

        [Fact(Skip = "Requires main chain user to be running.")]
        public void DoTest()
        {
            var transactionRequest = new BuildTransactionRequest()
            {
                FeeAmount = "0.01",
                // Change this to the address that should receive the funds.
                OpReturnData = "PLv2NAsyn22cNbk5veopWCkypaN6DBR27L",
                AccountName = "account 0",
                AllowUnconfirmed = true,
                Recipients = new List<RecipientModel> { new RecipientModel {
                    DestinationAddress = "2MyKFLbvhSouDYeAHhxsj9a5A4oV71j7SPR",
                    Amount = "1.1" } },
                Password = "test",
                WalletName = "test"
            };

            WalletBuildTransactionModel model = Post<BuildTransactionRequest, WalletBuildTransactionModel>(
                "http://127.0.0.1:38221/api/wallet/build-transaction", transactionRequest);

            var transaction = new PosTransaction(model.Hex);

            var reader = new OpReturnDataReader(this.loggerFactory, Networks.Stratis.Testnet());
            var extractor = new DepositExtractor(this.loggerFactory, this.federationGatewaySettings, reader);
            IDeposit deposit = extractor.ExtractDepositFromTransaction(transaction, 2, 1);

            Assert.NotNull(deposit);
            Assert.Equal(transaction.GetHash(), deposit.Id);
            Assert.Equal(transactionRequest.OpReturnData, deposit.TargetAddress);
            Assert.Equal(Money.Parse(transactionRequest.Recipients[0].Amount), deposit.Amount);
            Assert.Equal((uint256)1, deposit.BlockHash);
            Assert.Equal(2, deposit.BlockNumber);

            // Post the transaction
            var sendRequest = new SendTransactionRequest()
            {
                Hex = model.Hex
            };

            WalletSendTransactionModel model2 = Post<SendTransactionRequest, WalletSendTransactionModel>(
                "http://127.0.0.1:38221/api/wallet/send-transaction", sendRequest);

        }

        private Q Post<T,Q>(string url, T body)
        {
            // Request is sent to mainchain user.
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json";
            request.Method = "POST";

            var content = new JsonContent(body);

            string strContent = content.ReadAsStringAsync().GetAwaiter().GetResult();

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(strContent);
            }

            var response = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                string result = streamReader.ReadToEnd();
                return JsonConvert.DeserializeObject<Q>(result);
            }
        }
    }
}
