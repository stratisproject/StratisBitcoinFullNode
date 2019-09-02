using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class MultiSigTransactionsTests
    {
        /// <summary>
        /// This class creates all valid <c>true</c>/<c>false</c> combinations for input to <see cref="TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool, bool, bool, bool, bool, bool)"/>.
        /// </summary>
        private class TestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                for (int mask = 0; mask < 64; mask++)
                {
                    bool hasBlockHeight = (mask ^ 32) != 0;
                    bool hasSpendingDetails = (mask & 16) != 0;
                    bool hasWithdrawalDetails = (mask & 8) != 0;
                    bool flipBlockHeight = (mask & 4) != 0;
                    bool flipSpendingDetails = (mask & 2) != 0;
                    bool flipWithdrawalDetails = (mask & 1) != 0;

                    // Can't have withdrawal details without spending details.
                    if (!hasSpendingDetails && hasWithdrawalDetails)
                        continue;

                    if (!(hasSpendingDetails ^ flipSpendingDetails) && (hasWithdrawalDetails ^ flipWithdrawalDetails))
                        continue;

                    yield return new object[] { hasBlockHeight, hasSpendingDetails, hasWithdrawalDetails, flipBlockHeight, flipSpendingDetails, flipWithdrawalDetails };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Fact]
        public void CanChangeHeightOfSpendableTransaction()
        {
            var transactionData1 = new TransactionData()
            {
                Id = 1,
                Index = 1,
                BlockHeight = null,
                SpendingDetails = null
            };

            var transactionData2 = new TransactionData()
            {
                Id = 2,
                Index = 0,
                BlockHeight = null,
                SpendingDetails = null
            };

            var transactions = new MultiSigTransactions();
            transactions.Add(transactionData2);
            transactions.Add(transactionData1);

            transactionData2.BlockHeight = 1;

            transactions.Remove(transactionData2);

            transactionData2.BlockHeight = null;

            transactions.Add(transactionData2);
        }

        [Theory]
        [ClassData(typeof(TestData))]
        public void TransactionDataAddedToMultiSigTransactionsExistsInExpectedLookups(bool hasBlockHeight, bool hasSpendingDetails, bool hasWithdrawalDetails, bool flipBlockHeight, bool flipSpendingDetails, bool flipWithdrawalDetails)
        {
            uint256 transactionId = 1;
            int transactionIndex = 2;
            uint256 spendingTransactionId = 2;
            uint256 spendingDepositId = 3;

            SpendingDetails spendingDetails()
            {
                if (!hasSpendingDetails)
                    return null;

                return new SpendingDetails() {
                    WithdrawalDetails = hasWithdrawalDetails ? new WithdrawalDetails() {
                         MatchingDepositId = spendingDepositId
                    } : null,
                    BlockHeight = spendingBlockHeight(),
                    TransactionId = spendingTransactionId
                };
            }

            int? blockHeight()
            {
                return hasBlockHeight ? 3 : (int?)null;
            }

            int? spendingBlockHeight()
            {
                return hasBlockHeight ? 4 : (int?)null;
            }

            var transactionData = new TransactionData()
            {
                Id = transactionId,
                Index = transactionIndex,
                BlockHeight = blockHeight(),
                SpendingDetails = spendingDetails()
            };

            var transactions = new MultiSigTransactions();
            transactions.Add(transactionData);

            Assert.Contains(transactionData, transactions);

            void Validate()
            {
                if (hasBlockHeight && hasSpendingDetails)
                    Assert.Single(transactions.SpentTransactionsBeforeHeight(int.MaxValue), x => x.Item1 == spendingBlockHeight());
                else
                    Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));

                if (hasSpendingDetails && hasWithdrawalDetails)
                    Assert.Single(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);
                else
                    Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);

                if (hasSpendingDetails)
                    Assert.Empty(transactions.GetUnspentTransactions());
                else
                    Assert.Single(transactions.GetUnspentTransactions(), x => x.BlockHeight == blockHeight());
            }

            Validate();

            hasBlockHeight ^= flipBlockHeight;
            hasSpendingDetails ^= flipSpendingDetails;
            hasWithdrawalDetails ^= flipWithdrawalDetails;

            transactionData.BlockHeight = blockHeight();
            transactionData.SpendingDetails = spendingDetails();

            Validate();

            transactions.Remove(transactionData);

            Assert.Empty(transactions);
            Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));
            Assert.Empty(transactions.GetUnspentTransactions());
            Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).Single().txList);
        }

        [Fact]
        public void WhenSpendDetailsAreOverwrittenOldValuesAreCleanedUp()
        {
            var transactions = new MultiSigTransactions();
            uint256 tx1Hash = uint256.Parse("9903ea51b835faaf288fd1155f3b392525aa524f31f1fe964dd8c8503a34c705");
            uint256 deposit1Hash = uint256.Parse("72bc7d0217ca785e5ed662190ce00ebfabe7b79b92933df22d8f72968fdbdb74");
            uint256 spendingTx1Hash = uint256.Parse("7439f7933104aa57161ee46b575a5a1692d39cd83ab55f22ea58e39d57201556");

            var tx1 = new TransactionData
            {
                Amount = new Money(999965515551960),
                BlockHeight = null,
                BlockHash = null,
                Id = tx1Hash,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(1566808892),
                Index = 0
            };

            // simulate AddTransactionToWallet
            transactions.Add(tx1);

            // simulate AddSpendingTransactionToWallet
            transactions.TryGetTransaction(tx1Hash, 0, out TransactionData spentTransaction1);
            spentTransaction1.Should().NotBeNull();
            var spendingDetails1 = new SpendingDetails
            {
                WithdrawalDetails = new WithdrawalDetails { MatchingDepositId = deposit1Hash },
                BlockHeight = 1,
                TransactionId = spendingTx1Hash
            };
            spentTransaction1.SpendingDetails = spendingDetails1;

            uint256 tx2Hash = uint256.Parse("e771c55060b46d05e3bdf8b8b9dc7886395c08bab58e6c4618846af54afefcae");
            uint256 deposit2Hash = uint256.Parse("370e2d97459cef4029ea5d6728d933e8e63f6c0f78ead1f1f443f40cd54e7a8c");
            var tx2 = new TransactionData
            {
                Amount = new Money(999965515551960),
                BlockHeight = null,
                BlockHash = null,
                Id = tx2Hash,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(1566808892),
                Index = 0
            };

            transactions.Add(tx2);
            transactions.TryGetTransaction(tx2Hash, 0, out TransactionData spentTransaction2);
            spentTransaction2.Should().NotBeNull();
            var spendingDetails = new SpendingDetails()
            {
                WithdrawalDetails = new WithdrawalDetails { MatchingDepositId = deposit2Hash },
                BlockHeight = 1,
                TransactionId = spendingTx1Hash
            };
            spentTransaction2.SpendingDetails = spendingDetails;

            var withdrawals = new List<(Transaction transaction, IWithdrawal withdrawal)>();

            var txList = new List<TransactionData>();
            foreach ((uint256 _, List<TransactionData> txListDeposit) in transactions.GetSpendingTransactionsByDepositId(deposit1Hash))
            {
                txList.AddRange(txListDeposit);
            }

            Dictionary<TransactionData, Transaction> spendingTransactions = new Dictionary<TransactionData, Transaction>();//this.GetSpendingTransactions(txList);

            foreach (TransactionData txData in txList)
            {
                SpendingDetails spendingDetail = txData.SpendingDetails;

                // Multiple UTXOs may be spent by the one withdrawal, so if it's already added then no need to add it again.
                if (withdrawals.Any(w => w.withdrawal.Id == spendingDetail.TransactionId))
                    continue;

                var withdrawal = new Withdrawal(
                    spendingDetail.WithdrawalDetails.MatchingDepositId,
                    spendingDetail.TransactionId,
                    spendingDetail.WithdrawalDetails.Amount,
                    spendingDetail.WithdrawalDetails.TargetAddress,
                    spendingDetail.BlockHeight ?? 0,
                    spendingDetail.BlockHash);

                Transaction transaction = spendingTransactions[txData];

                withdrawals.Add((transaction, withdrawal));
            }

            foreach ((Transaction transaction, IWithdrawal withdrawal) in withdrawals)
            {
                
            }
            //uint256 transactionId = 1;
            //int transactionIndex = 2;
            //uint256 spendingTransactionId = 2;
            //uint256 spendingDepositId = 3;

            //SpendingDetails spendingDetails()
            //{
            //    if (!hasSpendingDetails)
            //        return null;

            //    return new SpendingDetails()
            //    {
            //        WithdrawalDetails = hasWithdrawalDetails ? new WithdrawalDetails()
            //        {
            //            MatchingDepositId = spendingDepositId
            //        } : null,
            //        BlockHeight = spendingBlockHeight(),
            //        TransactionId = spendingTransactionId
            //    };
            //}

            //int? blockHeight()
            //{
            //    return hasBlockHeight ? 3 : (int?)null;
            //}

            //int? spendingBlockHeight()
            //{
            //    return hasBlockHeight ? 4 : (int?)null;
            //}

            //var transactionData = new TransactionData()
            //{
            //    Id = transactionId,
            //    Index = transactionIndex,
            //    BlockHeight = blockHeight(),
            //    SpendingDetails = spendingDetails()
            //};

            //var transactions = new MultiSigTransactions();
            //transactions.Add(transactionData);

            //Assert.Contains(transactionData, transactions);

            //void Validate()
            //{
            //    if (hasBlockHeight && hasSpendingDetails)
            //        Assert.Single(transactions.SpentTransactionsBeforeHeight(int.MaxValue), x => x.Item1 == spendingBlockHeight());
            //    else
            //        Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));

            //    if (hasSpendingDetails && hasWithdrawalDetails)
            //        Assert.Single(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);
            //    else
            //        Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).First().txList);

            //    if (hasSpendingDetails)
            //        Assert.Empty(transactions.GetUnspentTransactions());
            //    else
            //        Assert.Single(transactions.GetUnspentTransactions(), x => x.BlockHeight == blockHeight());
            //}

            //Validate();

            //hasBlockHeight ^= flipBlockHeight;
            //hasSpendingDetails ^= flipSpendingDetails;
            //hasWithdrawalDetails ^= flipWithdrawalDetails;

            //transactionData.BlockHeight = blockHeight();
            //transactionData.SpendingDetails = spendingDetails();

            //Validate();

            //transactions.Remove(transactionData);

            //Assert.Empty(transactions);
            //Assert.Empty(transactions.SpentTransactionsBeforeHeight(int.MaxValue));
            //Assert.Empty(transactions.GetUnspentTransactions());
            //Assert.Empty(transactions.GetSpendingTransactionsByDepositId(spendingDepositId).Single().txList);
        }

        //private Dictionary<TransactionData, Transaction> GetSpendingTransactions(IEnumerable<TransactionData> transactions)
        //{
        //    var res = new Dictionary<TransactionData, Transaction>();

        //    // Record all the transaction data spent by a given spending transaction located in a given block.
        //    var spendTxsByBlockId = new Dictionary<uint256, Dictionary<uint256, List<TransactionData>>>();
        //    foreach (TransactionData transactionData in transactions)
        //    {
        //        SpendingDetails spendingDetail = transactionData.SpendingDetails;

        //        if (spendingDetail?.TransactionId == null || spendingDetail.Transaction != null)
        //        {
        //            res.Add(transactionData, spendingDetail?.Transaction);
        //            continue;
        //        }

        //        // Some SpendingDetail.BlockHash values may bet set to (uint256)0, so fix that too.
        //        if (spendingDetail.BlockHash == 0)
        //        {
        //            if (spendingDetail.BlockHeight == null || (spendingDetail.BlockHeight > this.chainIndexer.Tip.Height))
        //                continue;

        //            spendingDetail.BlockHash = this.chainIndexer[(int)spendingDetail.BlockHeight].HashBlock;
        //        }

        //        if (!spendTxsByBlockId.TryGetValue(spendingDetail.BlockHash, out Dictionary<uint256, List<TransactionData>> spentOutputsBySpendTxId))
        //        {
        //            spentOutputsBySpendTxId = new Dictionary<uint256, List<TransactionData>>();
        //            spendTxsByBlockId[spendingDetail.BlockHash] = spentOutputsBySpendTxId;
        //        }

        //        if (!spentOutputsBySpendTxId.TryGetValue(spendingDetail.TransactionId, out List<TransactionData> spentOutputs))
        //        {
        //            spentOutputs = new List<TransactionData>();
        //            spentOutputsBySpendTxId[spendingDetail.TransactionId] = spentOutputs;
        //        }

        //        spentOutputs.Add(transactionData);
        //    }

        //    // Will keep track of the height of spending details we're unable to fix.
        //    int firstMissingTransactionHeight = this.LastBlockSyncedHashHeight().Height + 1;

        //    // Find the spending transactions.
        //    foreach ((uint256 blockId, Dictionary<uint256, List<TransactionData>> spentOutputsBySpendTxId) in spendTxsByBlockId)
        //    {
        //        Block block = this.blockStore.GetBlock(blockId);
        //        Dictionary<uint256, Transaction> txIndex = block?.Transactions.ToDictionary(t => t.GetHash(), t => t);

        //        foreach ((uint256 spendTxId, List<TransactionData> spentOutputs) in spentOutputsBySpendTxId)
        //        {
        //            if (txIndex != null && txIndex.TryGetValue(spendTxId, out Transaction spendTransaction))
        //            {
        //                foreach (TransactionData transactionData in spentOutputs)
        //                    res[transactionData] = spendTransaction;
        //            }
        //            else
        //            {
        //                // The spending transaction could not be found in the consensus chain.
        //                // Set the firstMissingTransactionHeight to the block of the spending transaction.
        //                SpendingDetails spendingDetails = spentOutputs.Select(td => td.SpendingDetails).Where(s => s.BlockHeight != null).FirstOrDefault();

        //                Guard.Assert(spendingDetails != null);

        //                if (spendingDetails.BlockHeight < firstMissingTransactionHeight)
        //                    firstMissingTransactionHeight = (int)spendingDetails.BlockHeight;
        //            }
        //        }
        //    }

        //    // If there are unresolvable spending details then re-sync from that point onwards.
        //    if (firstMissingTransactionHeight <= this.LastBlockSyncedHashHeight().Height)
        //    {
        //        ChainedHeader fork = this.chainIndexer.GetHeader(Math.Min(firstMissingTransactionHeight - 1, this.chainIndexer.Height));

        //        this.RemoveBlocks(fork);
        //    }

        //    return res;
        //}
    }
}
