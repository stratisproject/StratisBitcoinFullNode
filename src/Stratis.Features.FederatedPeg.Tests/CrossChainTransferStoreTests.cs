using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Payloads;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Stratis.Sidechains.Networks;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class CrossChainTransferStoreTests : CrossChainTestBase
    {
        public CrossChainTransferStoreTests(Network network = null) : base(network)
        {
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWallet()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.wallet.LastBlockSyncedHeight == crossChainTransferStore.TipHashAndHeight.Height, $"LastBlockSyncedHeight:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.HashBlock);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithWalletAndSurvivesRestart()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                this.federationWalletManager.SaveWallet();

                TestBase.WaitLoopMessage(() => (this.wallet.LastBlockSyncedHeight == crossChainTransferStore.TipHashAndHeight.Height, $"LastBlockSyncedHeight:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.wallet.LastBlockSyncedHash, crossChainTransferStore.TipHashAndHeight.HashBlock);
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
        [Fact]
        public async Task StoringDepositsWhenWalletBalanceSufficientSucceedsWithDeterministicTransactionsAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

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

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                Transaction[] transactions = crossChainTransferStore.GetTransfersByStatus(new[] { CrossChainTransferStatus.Partial }).Select(x => x.PartialTransaction).ToArray();

                Assert.Equal(2, transactions.Length);

                // Transactions[0] inputs. Ordered deterministically, roughly a mixture of time and canonical ordering.
                Assert.Equal(2, transactions[0].Inputs.Count);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[0].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)1, transactions[0].Inputs[1].PrevOut.N);

                // Transaction[0] outputs.
                Assert.Equal(3, transactions[0].Outputs.Count);

                // Transaction[0] output value - change.
                Assert.Equal(new Money(10.00055999m, MoneyUnit.BTC), transactions[0].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[0].Outputs[0].ScriptPubKey);

                // Transaction[0] output value - recipient 1, but minus 0.001 for the tx fee and 0.01 for sender fee.
                Assert.Equal(new Money(159.999m, MoneyUnit.BTC), transactions[0].Outputs[1].Value);
                Assert.Equal(address1.ScriptPubKey, transactions[0].Outputs[1].ScriptPubKey);

                // Transaction[0] output value - op_return.
                Assert.Equal(new Money(1m, MoneyUnit.Satoshi), transactions[0].Outputs[2].Value);
                new OpReturnDataReader(this.loggerFactory, this.counterChainNetworkWrapper).TryGetTransactionId(transactions[0], out string actualDepositId);
                Assert.Equal(deposit1.Id.ToString(), actualDepositId);

                // Transactions[1] inputs.
                Assert.Single(transactions[1].Inputs);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change. Includes an extra 0.01 taken from sender deposit.
                Assert.Equal(new Money(10.00067999m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2, but minus 0.001 for the tx fee and 0.01 for sender fee.
                Assert.Equal(new Money(59.999m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(1m, MoneyUnit.Satoshi), transactions[1].Outputs[2].Value);
                new OpReturnDataReader(this.loggerFactory, this.counterChainNetworkWrapper).TryGetTransactionId(transactions[1], out string actualDepositId2);
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
        /// Transfers sending funds to contract addresses are marked as "Rejected".
        /// </summary>
        [Fact]
        public async void StoringDepositsWhenTargetIsContractFailsWithRejectedTransferAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

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

                KeyId p2pkhParams = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(address1.ScriptPubKey);
                var contractAddress = new uint160(p2pkhParams.ToBytes());

                this.stateRepositoryRoot.GetAccountState(contractAddress).Returns(new AccountState()); // not null

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer[] transfers = await crossChainTransferStore.GetAsync(new[] { deposit1.Id, deposit2.Id });

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Rejected, transfers[0].Status);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
            }
        }

        /// <summary>
        /// Recording deposits when the wallet UTXOs are sufficient succeeds with deterministic transactions.
        /// </summary>
        [Fact]
        public async Task StoringDepositsWhenWalletBalanceInSufficientSucceedsWithSuspendStatusAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                TestBase.WaitLoop(() => this.wallet.LastBlockSyncedHeight == this.ChainIndexer.Tip.Height);
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                uint256 txId1 = 0;
                uint256 txId2 = 1;
                uint256 blockHash = 2;

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(txId1, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, blockHash);
                var deposit2 = new Deposit(txId2, new Money(100m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, blockHash);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = blockHash,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { txId1, txId2 }).GetAwaiter().GetResult().ToArray();

                Transaction[] transactions = transfers.Select(t => t.PartialTransaction).ToArray();

                Assert.Equal(2, transactions.Length);

                // Transactions[0] inputs. Ordered deterministically.
                Assert.Equal(2, transactions[0].Inputs.Count);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[0].Inputs[0].PrevOut.N);
                Assert.Equal(this.fundingTransactions[0].GetHash(), transactions[0].Inputs[1].PrevOut.Hash);
                Assert.Equal((uint)1, transactions[0].Inputs[1].PrevOut.N);

                // Transaction[0] outputs.
                Assert.Equal(3, transactions[0].Outputs.Count);

                // Transaction[0] output value - Change + small profit. 2 UTXOS used as inputs. 1 satoshi for opreturn.
                Assert.Equal(new Money(10.00055999m, MoneyUnit.BTC), transactions[0].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[0].Outputs[0].ScriptPubKey);

                // Transaction[0] output value - recipient 1, but minus 0.001 for the constant tx fee.
                Assert.Equal(new Money(159.999m, MoneyUnit.BTC), transactions[0].Outputs[1].Value);
                Assert.Equal(address1.ScriptPubKey, transactions[0].Outputs[1].ScriptPubKey);

                // Transaction[0] output value - op_return.
                Assert.Equal(new Money(1m, MoneyUnit.Satoshi), transactions[0].Outputs[2].Value);
                new OpReturnDataReader(this.loggerFactory, this.counterChainNetworkWrapper).TryGetTransactionId(transactions[0], out string actualDepositId);
                Assert.Equal(deposit1.Id.ToString(), actualDepositId);

                Assert.Null(transactions[1]);

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(deposit1.Amount, new Money(transfers[0].DepositAmount));
                Assert.Equal(address1.ScriptPubKey, transfers[0].DepositTargetAddress);
                Assert.Equal(CrossChainTransferStatus.Suspended, transfers[1].Status);

                // Add more funds and resubmit the deposits.
                AddFundingTransaction(new Money[] { Money.COIN * 1000 });
                TestBase.WaitLoop(() => this.wallet.LastBlockSyncedHeight == this.ChainIndexer.Tip.Height);

                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                transfers = crossChainTransferStore.GetAsync(new uint256[] { txId1, txId2 }).GetAwaiter().GetResult().ToArray();
                transactions = transfers.Select(t => t.PartialTransaction).ToArray();

                // Transactions[1] inputs.
                Assert.Equal(2, transactions[1].Inputs.Count);
                Assert.Equal(this.fundingTransactions[1].GetHash(), transactions[1].Inputs[0].PrevOut.Hash);
                Assert.Equal((uint)0, transactions[1].Inputs[0].PrevOut.N);

                // Transaction[1] outputs.
                Assert.Equal(3, transactions[1].Outputs.Count);

                // Transaction[1] output value - change.
                Assert.Equal(new Money(970.00055999m, MoneyUnit.BTC), transactions[1].Outputs[0].Value);
                Assert.Equal(multiSigAddress.ScriptPubKey, transactions[1].Outputs[0].ScriptPubKey);

                // Transaction[1] output value - recipient 2, but minus 0.001 for the tx fee and 0.01 for sender fee.
                Assert.Equal(new Money(99.999m, MoneyUnit.BTC), transactions[1].Outputs[1].Value);
                Assert.Equal(address2.ScriptPubKey, transactions[1].Outputs[1].ScriptPubKey);

                // Transaction[1] output value - op_return.
                Assert.Equal(new Money(1m, MoneyUnit.Satoshi), transactions[1].Outputs[2].Value);
                new OpReturnDataReader(this.loggerFactory, this.counterChainNetworkWrapper).TryGetTransactionId(transactions[1], out string actualDepositId2);
                Assert.Equal(deposit2.Id.ToString(), actualDepositId2);

                Assert.Equal(2, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(deposit2.Amount, new Money(transfers[1].DepositAmount));
                Assert.Equal(address2.ScriptPubKey, transfers[1].DepositTargetAddress);

                (Money confirmed, Money unconfirmed) spendable = this.federationWalletManager.GetSpendableAmount();

                // Includes ~0.0012 taken from deposit amounts - our profit.
                Assert.Equal(new Money(980.00111998m, MoneyUnit.BTC), spendable.unconfirmed);
            }
        }

        /// <summary>
        /// Test that if one transaction is set to suspended then all following transactions will be too to maintain deterministic order.
        /// </summary>
        [Fact]
        public async Task SetAllAfterSuspendedToSuspendedAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                TestBase.WaitLoop(() => this.wallet.LastBlockSyncedHeight == this.ChainIndexer.Tip.Height);
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                uint256 txId1 = 0;
                uint256 txId2 = 1;
                uint256 txId3 = 2;
                uint256 blockHash = 2;

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address3 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(txId1, new Money(1m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, blockHash);
                var deposit2 = new Deposit(txId2, new Money(2m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, blockHash);
                var deposit3 = new Deposit(txId3, new Money(3m, MoneyUnit.BTC), address3.ToString(), crossChainTransferStore.NextMatureDepositHeight, blockHash);


                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = blockHash,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2, deposit3 })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer[] transfers = crossChainTransferStore.GetAsync(new uint256[] { txId1, txId2, txId3 }).GetAwaiter().GetResult().ToArray();

                Assert.Equal(3, transfers.Length);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[0].Status);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[1].Status);
                Assert.Equal(CrossChainTransferStatus.Partial, transfers[2].Status);

                // Lets break the first transaction
                this.federationWalletManager.RemoveWithdrawalTransactions(deposit1.Id);

                // Transactions after will be broken
                transfers = crossChainTransferStore.GetAsync(new uint256[] { txId1, txId2, txId3 }).GetAwaiter().GetResult().ToArray();
                Assert.Equal(CrossChainTransferStatus.Suspended, transfers[0].Status);
                Assert.Equal(CrossChainTransferStatus.Suspended, transfers[1].Status);
                Assert.Equal(CrossChainTransferStatus.Suspended, transfers[2].Status);
            }
        }

        /// <summary>
        /// Tests whether the store merges signatures as expected.
        /// </summary>
        [Fact]
        public async Task StoreMergesSignaturesAsExpectedAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore cctsInstanceOne = this.CreateStore())
            {
                cctsInstanceOne.Initialize();
                cctsInstanceOne.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == cctsInstanceOne.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{cctsInstanceOne.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, cctsInstanceOne.TipHashAndHeight.HashBlock);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit = new Deposit(1, new Money(160m, MoneyUnit.BTC), address.ToString(), cctsInstanceOne.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = cctsInstanceOne.NextMatureDepositHeight },
                    new[] { deposit })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await cctsInstanceOne.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer crossChainTransfer = cctsInstanceOne.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction partialTransaction = crossChainTransfer.PartialTransaction;

                Assert.True(cctsInstanceOne.ValidateTransaction(partialTransaction));

                // Create a separate instance to generate another transaction.
                Transaction transaction2;
                var testInstanceTwo = new CrossChainTransferStoreTests(this.network);
                var dataFolderTwo = new DataFolder(TestBase.CreateTestDir(this));

                testInstanceTwo.federationKeys = this.federationKeys;
                testInstanceTwo.SetExtendedKey(1);
                testInstanceTwo.Init(dataFolderTwo);

                // Clone chain
                for (int i = 1; i <= this.ChainIndexer.Height; i++)
                {
                    ChainedHeader header = this.ChainIndexer.GetHeader(i);
                    Block block = this.blockDict[header.HashBlock];
                    testInstanceTwo.AppendBlock(block);
                }

                using (ICrossChainTransferStore cctsInstanceTwo = testInstanceTwo.CreateStore())
                {
                    cctsInstanceTwo.Initialize();
                    cctsInstanceTwo.Start();

                    Assert.Equal(testInstanceTwo.ChainIndexer.Tip.HashBlock, cctsInstanceTwo.TipHashAndHeight.HashBlock);
                    Assert.Equal(testInstanceTwo.ChainIndexer.Tip.Height, cctsInstanceTwo.TipHashAndHeight.Height);

                    RecordLatestMatureDepositsResult recordMatureDepositResult2 = await cctsInstanceTwo.RecordLatestMatureDepositsAsync(blockDeposits);

                    ICrossChainTransfer crossChainTransfer2 = cctsInstanceTwo.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                    Assert.NotNull(crossChainTransfer2);

                    transaction2 = crossChainTransfer2.PartialTransaction;

                    Assert.True(cctsInstanceTwo.ValidateTransaction(transaction2));
                }

                // Merges the transaction signatures.
                Transaction mergedTransaction = await cctsInstanceOne.MergeTransactionSignaturesAsync(deposit.Id, new[] { transaction2 });

                // Test the outcome.
                crossChainTransfer = cctsInstanceOne.GetAsync(new[] { deposit.Id }).GetAwaiter().GetResult().SingleOrDefault();

                Assert.NotNull(crossChainTransfer);
                Assert.Equal(CrossChainTransferStatus.FullySigned, crossChainTransfer.Status);

                // Should be returned as signed.
                Transaction signedTransaction = cctsInstanceOne.GetTransfersByStatus(new[] { CrossChainTransferStatus.FullySigned }).Select(x => x.PartialTransaction).SingleOrDefault();

                Assert.NotNull(signedTransaction);

                // Check ths signature.
                Assert.True(cctsInstanceOne.ValidateTransaction(signedTransaction, true));
            }
        }

        /// <summary>
        /// Check that partial transactions present in the store cause partial transaction requests made to peers.
        /// </summary>
        [Fact]
        public async Task StoredPartialTransactionsTriggerSignatureRequestAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit1 = new Deposit(1, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                var deposit2 = new Deposit(2, new Money(60m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer[] transactions = crossChainTransferStore.GetTransfersByStatus(new[] { CrossChainTransferStatus.Partial });

                var requester = new PartialTransactionRequester(this.loggerFactory, crossChainTransferStore, this.asyncProvider,
                    this.nodeLifetime, this.federatedPegBroadcaster, this.ibdState, this.federationWalletManager, this.inputConsolidator);

                requester.Start();

                Thread.Sleep(2000);

                // Receives all of the requests. We broadcast multiple at a time.
                this.federatedPegBroadcaster.Received().BroadcastAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                        o.DepositId == 1 && o.PartialTransaction.GetHash() ==
                        transactions[0].PartialTransaction.GetHash()))
                    .GetAwaiter().GetResult();

                this.federatedPegBroadcaster.Received().BroadcastAsync(Arg.Is<RequestPartialTransactionPayload>(o =>
                        o.DepositId == 2 && o.PartialTransaction.GetHash() ==
                        transactions[1].PartialTransaction.GetHash()))
                    .GetAwaiter().GetResult();
            }
        }

        [Fact]
        public void NextMatureDepositStartsHigherOnMain()
        {
            // This should really be 2 tests in separate classes but we'll fit in with what is already happening for now.

            // Start querying counter-chain for deposits from first non-genesis block on main chain and a higher number on side chain.
            int depositHeight = (this.network.Name == new StratisRegTest().Name)
                ? 1
                : FederatedPegSettings.StratisMainDepositStartBlock;

            this.federatedPegSettings.CounterChainDepositStartBlock.Returns(depositHeight);

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

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

            var reader = new OpReturnDataReader(this.loggerFactory, new CounterChainNetworkWrapper(CirrusNetwork.NetworksSelector.Testnet()));
            var extractor = new DepositExtractor(this.loggerFactory, this.federatedPegSettings, reader);
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

        /// <summary>
        /// Attempt to get the federation to merge signatures on an invalid transaction that sends federation UTXOs to its own address.
        /// Simulates the behaviour if someone were to come on the network and broadcast their own <see cref="RequestPartialTransactionPayload"/> message
        /// with bogus information.
        /// </summary>
        [Fact]
        public async Task AttemptFederationInvalidWithdrawalAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                var deposit = new Deposit(0, new Money(160m, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                ICrossChainTransfer[] crossChainTransfers = await crossChainTransferStore.GetAsync(new[] { deposit.Id });
                ICrossChainTransfer crossChainTransfer = crossChainTransfers.SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                Transaction partialTransaction = crossChainTransfer.PartialTransaction;

                Assert.True(crossChainTransferStore.ValidateTransaction(partialTransaction));

                crossChainTransfers = await crossChainTransferStore.GetAsync(new[] { deposit.Id });
                ICrossChainTransfer crossChainTransfer2 = crossChainTransfers.SingleOrDefault();

                Assert.NotNull(crossChainTransfer2);

                // Modify transaction 2 to send the funds to a new address.
                BitcoinAddress bogusAddress = new Key().PubKey.Hash.GetAddress(this.network);

                Transaction transaction2 = crossChainTransfer2.PartialTransaction;

                transaction2.Outputs[1].ScriptPubKey = bogusAddress.ScriptPubKey;

                // Merges the transaction signatures.
                await crossChainTransferStore.MergeTransactionSignaturesAsync(deposit.Id, new[] { transaction2 });

                // Test the outcome.
                crossChainTransfers = await crossChainTransferStore.GetAsync(new[] { deposit.Id });
                crossChainTransfer = crossChainTransfers.SingleOrDefault();

                Assert.NotNull(crossChainTransfer);

                // Expect signing to fail.
                Assert.NotEqual(CrossChainTransferStatus.FullySigned, crossChainTransfer.Status);

                // Should return null.
                ICrossChainTransfer[] signedTransactions = crossChainTransferStore.GetTransfersByStatus(new[] { CrossChainTransferStatus.FullySigned });
                Transaction signedTransaction = signedTransactions.Select(x => x.PartialTransaction).SingleOrDefault();

                Assert.Null(signedTransaction);
            }
        }

        /// <summary>
        /// Recording deposits when the wallet UTXOs are sufficient succeeds with deterministic transactions.
        /// </summary>
        [Fact]
        public async Task StoringDepositsAfterRewindIsPrecededByClearingInvalidTransientsAndSettingNextMatureDepositHeightCorrectlyAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);

            // Creates two consecutive blocks of funding transactions with 100 coins each.
            (Transaction fundingTransaction1, ChainedHeader fundingBlock1) = AddFundingTransaction(new Money[] { Money.COIN * 100 });
            (Transaction fundingTransaction2, ChainedHeader fundingBlock2) = AddFundingTransaction(new Money[] { Money.COIN * 100 });

            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                BitcoinAddress address1 = (new Key()).PubKey.Hash.GetAddress(this.network);
                BitcoinAddress address2 = (new Key()).PubKey.Hash.GetAddress(this.network);

                // First deposit.
                var deposit1 = new Deposit(1, new Money(100m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposit1 = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1 })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposit1);

                ICrossChainTransfer transfer1 = crossChainTransferStore.GetAsync(new[] { deposit1.Id }).GetAwaiter().GetResult().FirstOrDefault();
                Assert.Equal(CrossChainTransferStatus.Partial, transfer1?.Status);

                // Second deposit.
                var deposit2 = new Deposit(2, new Money(100m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 2);

                MaturedBlockDepositsModel[] blockDeposit2 = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 2,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit2 })
                };

                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposit2);

                ICrossChainTransfer transfer2 = crossChainTransferStore.GetAsync(new[] { deposit2.Id }).GetAwaiter().GetResult().FirstOrDefault();
                Assert.Equal(CrossChainTransferStatus.Partial, transfer2?.Status);

                // Both partial transactions have been created. Now rewind the wallet.
                this.ChainIndexer.SetTip(fundingBlock1);
                this.federationWalletSyncManager.ProcessBlock(fundingBlock1.Block);

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == this.federationWalletSyncManager.WalletTip.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} SyncManager.TipHashHeight:{this.federationWalletSyncManager.WalletTip.Height}"));

                // Synchronize the store using a dummy get.
                crossChainTransferStore.GetAsync(new uint256[] { }).GetAwaiter().GetResult();

                // See if the NextMatureDepositHeight was rewound for the replay of deposit 2.
                Assert.Equal(deposit2.BlockNumber, crossChainTransferStore.NextMatureDepositHeight);

                // That's great. Now let's redo deposit 2 which had its funding wiped out.
                (fundingTransaction2, fundingBlock2) = AddFundingTransaction(new Money[] { Money.COIN * 100 });

                // Ensure that the new funds are mature.
                this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

                // Recreate the second deposit.
                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposit2);

                // Check that its status is partial.
                transfer2 = crossChainTransferStore.GetAsync(new[] { deposit2.Id }).GetAwaiter().GetResult().FirstOrDefault();
                Assert.Equal(CrossChainTransferStatus.Partial, transfer2?.Status);

                Assert.Equal(4, this.wallet.MultiSigAddress.Transactions.Count);
            }
        }

        /// <summary>
        /// Test demonstrates what happens when there is a reorg. Specifically, that no FullySigned transactions are maintained,
        /// even though we previously tried to do so.
        /// </summary>
        [Fact]
        public async Task ReorgSetsAllInProgressToSuspended()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (
                    this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height,
                    $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);


                // Make 10 deposits
                const int numDeposits = 10;
                const decimal depositSend = 1;

                Deposit[] deposits = new Deposit[numDeposits];
                BitcoinAddress address = new Script("").Hash.GetAddress(this.network);

                for (int i = 0; i < numDeposits; i++)
                {
                    deposits[i] = new Deposit((ulong)i, new Money(depositSend, MoneyUnit.BTC), address.ToString(),
                        crossChainTransferStore.NextMatureDepositHeight, 1);
                }

                Money[] funding = new Money[numDeposits - this.fundingTransactions.Count];

                for (int i = 0; i < funding.Length; i++)
                {
                    funding[i] = new Money(depositSend, MoneyUnit.BTC);
                }

                (Transaction, ChainedHeader header) added = this.AddFundingTransaction(funding);

                var blockDeposits = new Dictionary<int, MaturedBlockDepositsModel[]>();

                blockDeposits[crossChainTransferStore.NextMatureDepositHeight] = new[]
                {
                    new MaturedBlockDepositsModel(
                        new MaturedBlockInfoModel
                        {
                            BlockHash = 1,
                            BlockHeight = crossChainTransferStore.NextMatureDepositHeight
                        },
                        deposits)
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult =
                    await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                // Create 1 block with all 10 withdrawals inside.
                ChainedHeader header = this.AppendBlock(recordMatureDepositResult.WithDrawalTransactions.ToArray());

                // Check that CCTS now has 10 withdrawals that are SeenInBlock.
                ICrossChainTransfer[] seenInBlock =
                    crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[]
                        {CrossChainTransferStatus.SeenInBlock});
                Assert.Equal(numDeposits, seenInBlock.Length);

                // Sync our CCTS
                // TODO: This does nothing. Remove?
                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[1]);

                // Lets make 10 more deposits using the change UTXOS in the block just gone.
                Deposit[] moreDeposits = new Deposit[numDeposits];
                for (int i = 0; i < numDeposits; i++)
                {
                    ulong newId = (ulong)numDeposits + (ulong)i; // to get a unique ID.
                    moreDeposits[i] = new Deposit(newId, new Money(depositSend, MoneyUnit.BTC), address.ToString(),
                        crossChainTransferStore.NextMatureDepositHeight, 2);
                }

                blockDeposits[crossChainTransferStore.NextMatureDepositHeight] = new[]
                {
                    new MaturedBlockDepositsModel(
                        new MaturedBlockInfoModel
                        {
                            BlockHash = 2,
                            BlockHeight = crossChainTransferStore.NextMatureDepositHeight
                        },
                        moreDeposits)
                };

                recordMatureDepositResult =
                    await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                // We built more transctions with the UTXOs included in a block...
                Assert.True(recordMatureDepositResult.WithDrawalTransactions.Count > 0);

                int expectedPartials = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.Partial }).Length;
                int expectedSuspends = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.Suspended }).Length;
                int expectedSeenInBlocks = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.SeenInBlock }).Length;

                // Now lets rewind.
                this.ChainIndexer.SetTip(added.header);
                this.federationWalletSyncManager.ProcessBlock(added.header.Block);
                TestBase.WaitLoop(() => this.federationWalletManager.WalletTipHash == this.ChainIndexer.Tip.HashBlock);

                // If we were able to keep FullySigned transactions then we would have FullySigned after this rewind.
                ICrossChainTransfer[] fullySigned = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.FullySigned });

                // However we have none.
                Assert.Empty(fullySigned);

                // We do have 20 Suspended transactions now though.
                ICrossChainTransfer[] suspended = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.Suspended });
                Assert.Equal(20, suspended.Length);

                // See if we will recover to the point where the reorg occurred.
                while (crossChainTransferStore.NextMatureDepositHeight <= blockDeposits.Max(kv => kv.Key))
                {
                    recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(
                        blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                    if (!recordMatureDepositResult.MatureDepositRecorded)
                        break;

                    // Makes the withdrawals seen and makes their UTXOs spendable.
                    this.AppendBlock(recordMatureDepositResult.WithDrawalTransactions.ToArray());
                }

                // Verify our expectations.
                Assert.Equal(expectedPartials, crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.Partial }).Length);
                Assert.Equal(expectedSuspends, crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.Suspended }).Length);
                Assert.Equal(expectedSeenInBlocks, crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.SeenInBlock }).Length);
            }
        }

        [Fact]
        public async Task ReorgDoesntLeaveBehindUnconfirmedTransactions()
        {
            const int numDeposits = 10;
            const int numDeposits2 = 5;
            const decimal fundingAmount = 100;
            const decimal depositAmount = 1;

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (
                    this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height,
                    $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                // Get rid of the pre-existing transactions. It's easier to track with 10 of our own utxos.
                this.fundingTransactions.Clear();

                foreach (TransactionData tx in this.wallet.MultiSigAddress.Transactions.ToList())
                {
                    this.wallet.MultiSigAddress.Transactions.Remove(tx);
                }

                // Make our own 10 utxos.
                Money[] funding = new Money[numDeposits];

                for (int i = 0; i < funding.Length; i++)
                {
                    funding[i] = new Money(fundingAmount, MoneyUnit.BTC);
                }

                // Make 10 deposits
                Deposit[] deposits = new Deposit[numDeposits];
                BitcoinAddress address = new Script("").Hash.GetAddress(this.network);

                for (int i = 0; i < numDeposits; i++)
                {
                    deposits[i] = new Deposit((ulong)i, new Money(depositAmount, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                }

                (Transaction, ChainedHeader header) added = this.AddFundingTransaction(funding);

                var blockDeposits = new Dictionary<int, MaturedBlockDepositsModel[]>();

                blockDeposits[crossChainTransferStore.NextMatureDepositHeight] = new[]
                {
                    new MaturedBlockDepositsModel(
                        new MaturedBlockInfoModel
                        {
                            BlockHash = 1,
                            BlockHeight = crossChainTransferStore.NextMatureDepositHeight
                        },
                        deposits)
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult =
                    await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                Assert.Equal(numDeposits, recordMatureDepositResult.WithDrawalTransactions.Count);

                // Create 1 block with all 10 withdrawals inside.
                ChainedHeader header = this.AppendBlock(recordMatureDepositResult.WithDrawalTransactions.ToArray());

                // Check that CCTS now has 10 withdrawals that are SeenInBlock.
                ICrossChainTransfer[] seenInBlock = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.SeenInBlock });
                Assert.Equal(numDeposits, seenInBlock.Length);

                // Sync our CCTS
                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[1]);

                // Lets make 10 more deposits using the change UTXOS in the block just gone.
                Deposit[] moreDeposits = new Deposit[numDeposits2];
                for (int i = 0; i < numDeposits2; i++)
                {
                    ulong newId = (ulong)numDeposits + (ulong)i; // to get a unique ID.
                    moreDeposits[i] = new Deposit(newId, new Money(depositAmount, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 2);
                }

                blockDeposits[crossChainTransferStore.NextMatureDepositHeight] = new[]
                {
                    new MaturedBlockDepositsModel(
                        new MaturedBlockInfoModel
                        {
                            BlockHash = 2,
                            BlockHeight = crossChainTransferStore.NextMatureDepositHeight
                        },
                        moreDeposits)
                };

                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                // We built more transctions with the UTXOs included in a block...
                Assert.Equal(numDeposits2, recordMatureDepositResult.WithDrawalTransactions.Count);

                // Now lets rewind.
                this.ChainIndexer.SetTip(added.header);
                this.federationWalletSyncManager.ProcessBlock(added.header.Block);
                TestBase.WaitLoop(() => this.federationWalletManager.WalletTipHash == this.ChainIndexer.Tip.HashBlock);

                // Instead of a block full of 10 each this time though, lets do 5 of 2 each, and with the transactions being picked in reverse order.
                // This mimics possible real-world scenarios where all txs are signed, but not necessarily picked in the same order by the miner,
                // and will mean that the order of the UTXOS picked for withdrawals will be different on this new chain.

                // We do this to get the CCTS to reorg also
                crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.SeenInBlock });
                //recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                // And then again to actually create the transactions again.
                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);
                Assert.Equal(numDeposits, recordMatureDepositResult.WithDrawalTransactions.Count);

                recordMatureDepositResult.WithDrawalTransactions.Reverse();

                for (int i = 0; i < numDeposits / 2; i++)
                {
                    header = this.AppendBlock(recordMatureDepositResult.WithDrawalTransactions.Skip(i * 2).Take(2).ToArray());
                }

                // Now lets put the second group of 5 transactions into a single block on our new chain.
                recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);
                Assert.Equal(numDeposits2, recordMatureDepositResult.WithDrawalTransactions.Count);

                header = this.AppendBlock(recordMatureDepositResult.WithDrawalTransactions.ToArray());

                // Everything should be confirmed and in a block.
                seenInBlock = crossChainTransferStore.GetTransfersByStatus(new CrossChainTransferStatus[] { CrossChainTransferStatus.SeenInBlock });
                Assert.Equal(numDeposits + numDeposits2, seenInBlock.Length);

                // Everything is in a block - we shouldn't have any lingering unconfirmed transactions.
                var unconfirmedTransactions = this.wallet.MultiSigAddress.Transactions.Where(x => x.SpendingDetails == null && x.BlockHeight == null).ToList();
                Assert.Empty(unconfirmedTransactions);
            }
        }

        [Fact]
        public async Task CrossChainTransferStoreDoesntCreateMassiveTransactions()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (
                    this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height,
                    $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                // Lets set the funding transactions to many really small outputs
                const int numUtxos = FederatedPegSettings.MaxInputs * 2;
                const decimal individualAmount = 0.1m;
                const decimal depositAmount = numUtxos * individualAmount - 1; // Large amount minus some for fees.
                BitcoinAddress address = new Script("").Hash.GetAddress(this.network);

                this.wallet.MultiSigAddress.Transactions.Clear();
                this.fundingTransactions.Clear();

                Money[] funding = new Money[numUtxos];

                for (int i = 0; i < funding.Length; i++)
                {
                    funding[i] = new Money(individualAmount, MoneyUnit.BTC);
                }

                this.AddFundingTransaction(funding);

                Deposit deposit = new Deposit(1uL, new Money(depositAmount, MoneyUnit.BTC), address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                var blockDeposits = new Dictionary<int, MaturedBlockDepositsModel[]>();

                blockDeposits[crossChainTransferStore.NextMatureDepositHeight] = new[]
                {
                    new MaturedBlockDepositsModel(
                        new MaturedBlockInfoModel
                        {
                            BlockHash = 1,
                            BlockHeight = crossChainTransferStore.NextMatureDepositHeight
                        },
                        new []{deposit})
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits[crossChainTransferStore.NextMatureDepositHeight]);

                // The CCTS won't create any transactions until the InputConsolidator consolidates some inputs
                Assert.Empty(recordMatureDepositResult.WithDrawalTransactions);

                this.signals.Received().Publish(Arg.Any<WalletNeedsConsolidation>());
            }
        }

        [Fact]
        public async Task WalletSyncFromHeightOverridesWalletLastBlockSyncedHeight()
        {
            // Only sync the wallet from the second funding block.
            this.federatedPegSettings.WalletSyncFromHeight.Returns(2);

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);

            FederationWallet wallet = this.federationWalletManager.GetWallet();

            // LastBlockSyncedHeight = WalletSyncFromHeight - 1.
            Assert.Equal(1, wallet.LastBlockSyncedHeight);

            // Add 2 blocks with 2 and 1 transactions respectively.
            this.AddFunding();

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();

                // Only the second block containing 1 transaction should be processed.
                Assert.Equal(1, wallet.MultiSigAddress.Transactions.Count);
            }
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

        /// <summary>
        /// Recording deposits when the target is our multisig is ignored, but a different multisig is allowed.
        /// </summary>
        [Fact]
        public async Task StoringDepositsWhenTargetIsMultisigIsIgnoredIffOurMultisigAsync()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            this.Init(dataFolder);
            this.AddFunding();
            this.AppendBlocks(WithdrawalTransactionBuilder.MinConfirmations);

            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            using (ICrossChainTransferStore crossChainTransferStore = this.CreateStore())
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                TestBase.WaitLoopMessage(() => (this.ChainIndexer.Tip.Height == crossChainTransferStore.TipHashAndHeight.Height, $"ChainIndexer.Height:{this.ChainIndexer.Tip.Height} Store.TipHashHeight:{crossChainTransferStore.TipHashAndHeight.Height}"));
                Assert.Equal(this.ChainIndexer.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.HashBlock);

                // Forwarding money already in the multisig address to the multisig address is ignored.
                BitcoinAddress address1 = multiSigAddress.RedeemScript.Hash.GetAddress(this.network);
                BitcoinAddress address2 = new Script("").Hash.GetAddress(this.network);

                var deposit1 = new Deposit(0, new Money(160m, MoneyUnit.BTC), address1.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);
                var deposit2 = new Deposit(1, new Money(160m, MoneyUnit.BTC), address2.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                MaturedBlockDepositsModel[] blockDeposits = new[] { new MaturedBlockDepositsModel(
                    new MaturedBlockInfoModel() {
                        BlockHash = 1,
                        BlockHeight = crossChainTransferStore.NextMatureDepositHeight },
                    new[] { deposit1, deposit2 })
                };

                RecordLatestMatureDepositsResult recordMatureDepositResult = await crossChainTransferStore.RecordLatestMatureDepositsAsync(blockDeposits);

                Transaction[] partialTransactions = crossChainTransferStore.GetTransfersByStatus(new[] { CrossChainTransferStatus.Partial }).Select(x => x.PartialTransaction).ToArray();
                Transaction[] suspendedTransactions = crossChainTransferStore.GetTransfersByStatus(new[] { CrossChainTransferStatus.Suspended }).Select(x => x.PartialTransaction).ToArray();

                // Only the deposit going towards a different multisig address is accepted. The other is ignored.
                Assert.Single(partialTransactions);
                Assert.Empty(suspendedTransactions);

                IWithdrawal withdrawal = this.withdrawalExtractor.ExtractWithdrawalFromTransaction(partialTransactions[0], null, 1);
                Assert.Equal((uint256)1, withdrawal.DepositId);
            }
        }

        /// <summary>
        /// <see cref="CrossChainTransferStore.IsMempoolErrorRecoverable(MempoolError)"/> returns appropriate responses for different types of errors.
        /// </summary>
        [Fact]
        public void IsMempoolErrorRecoverableReturnsTrueForRecoverableErrors()
        {
            Assert.True(CrossChainTransferStore.IsMempoolErrorRecoverable(new MempoolError()));
            Assert.True(CrossChainTransferStore.IsMempoolErrorRecoverable(new MempoolError() { RejectCode = MempoolErrors.RejectDuplicate }));
            Assert.True(CrossChainTransferStore.IsMempoolErrorRecoverable(new MempoolError() { RejectCode = MempoolErrors.RejectAlreadyKnown }));
            Assert.False(CrossChainTransferStore.IsMempoolErrorRecoverable(new MempoolError()
            {
                ConsensusError = new ConsensusError("p2pkh-to-contract", "attempted send directly to contract address. use OP_CALL instead.")
            }));
        }
    }
}
