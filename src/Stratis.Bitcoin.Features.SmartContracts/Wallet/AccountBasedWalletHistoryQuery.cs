using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    /// <summary>
    /// Generates history for an accounts-based wallet system. In an accounts-based wallet system,
    /// a single address is used to send and receive transactions and change.
    ///
    /// The key difference with "regular" wallet history is that this query assumes that all change is being returned to the sender address,
    /// and will return the amount as the total of all outputs minus any assumed change.
    /// </summary>
    public class AccountBasedWalletHistoryQuery
    {
        public const int MaxHistoryItems = 1000;

        public List<TransactionItemModel> GetHistory(IEnumerable<FlatHistory> accountHistory, string address, int? skip = null, int? take = null)
        {
            var transactionItems = new List<TransactionItemModel>();
            var uniqueProcessedTxIds = new HashSet<uint256>();

            // We need to order accountHistory by creation time so that spending details always appear
            // ahead of the transactions that spend them.
            var items = accountHistory
                .Where(x => x.Address.Address == address)
                .OrderBy(x => x.Transaction.CreationTime)
                .ToList();

            // Create a lookup to return the inputs for a particular transaction ID.
            ILookup<uint256, TransactionData> transactionInputs = items
                .Where(t => t.Transaction.SpendingDetails != null)
                .ToLookup(s => s.Transaction.SpendingDetails.TransactionId, s => s.Transaction);

            // Spending details can occur multiple times and we only want to process them once.
            var processedSpendingDetails = new HashSet<uint256>();

            // Store known change transactions.
            var changePayments = new List<ChangePayment>();

            foreach (FlatHistory historyItem in items)
            {
                if (ProcessingLimitExceeded(uniqueProcessedTxIds))
                    break;

                TransactionData transaction = historyItem.Transaction;

                // Ignore any change payments as received items.
                // It's "change" if it was received as the change output of another transaction.
                // We have to match by transaction ID and the index of the change output in the transaction.
                if (!changePayments.Any(p => p.IsChange(historyItem.Transaction)))
                {
                    // Add incoming fund transaction details.
                    var receivedItem = new TransactionItemModel
                    {
                        Type = TransactionItemType.Received,
                        ToAddress = historyItem.Address.Address,
                        Amount = transaction.Amount,
                        Id = transaction.Id,
                        Timestamp = transaction.CreationTime,
                        ConfirmedInBlock = transaction.BlockHeight,
                        BlockIndex = transaction.BlockIndex
                    };

                    transactionItems.Add(receivedItem);
                    uniqueProcessedTxIds.Add(receivedItem.Id);
                }

                SpendingDetails spendingDetail = transaction.SpendingDetails;

                if (spendingDetail == null)
                    continue;

                // Don't process same spending details twice.
                if (processedSpendingDetails.Contains(spendingDetail.TransactionId))
                    continue;

                processedSpendingDetails.Add(spendingDetail.TransactionId);

                // We need to make a copy of the list here because it seems the wallet changes the underlying
                // objects here which breaks reference equality checks later.
                var payments = spendingDetail.Payments.ToList();

                PaymentDetails changePayment = payments.ChangePaymentOrDefault(address);

                var hasChangePayment = changePayment != null;

                if (hasChangePayment)
                {
                    changePayments.Add(new ChangePayment(spendingDetail, changePayment));
                }

                var paymentDetails = new List<PaymentDetailModel>();
                Money amount = 0;

                foreach (PaymentDetails payment in payments.Where(payment => payment != changePayment))
                {
                    paymentDetails.Add(new PaymentDetailModel
                    {
                        DestinationAddress = payment.DestinationAddress,
                        Amount = payment.Amount
                    });

                    amount += payment.Amount;
                }

                uint256 spendingTransactionId = spendingDetail.TransactionId;

                var inputAmount = transactionInputs[spendingTransactionId].Sum(t => t.Amount);

                var sentItem = new TransactionItemModel
                {
                    Type = TransactionItemType.Send,
                    Id = spendingTransactionId,
                    Timestamp = spendingDetail.CreationTime,
                    ConfirmedInBlock = spendingDetail.BlockHeight,
                    BlockIndex = spendingDetail.BlockIndex,
                    Amount = amount,
                    Payments = paymentDetails,
                    Fee = inputAmount - payments.Sum(p => p.Amount)
                };

                transactionItems.Add(sentItem);
                uniqueProcessedTxIds.Add(spendingTransactionId);
            }

            // Sort and filter the history items.
            // Ordering by Type is required so that sends appear earlier than receives eg. if a send occurs with the same timestamp as a receive.
            List<TransactionItemModel> itemsToInclude = transactionItems
                .OrderBy(o => o.ConfirmedInBlock.HasValue ? 1 : 0)
                .ThenByDescending(o => o.Timestamp)
                .ThenBy(o => o.Type)
                .Skip(skip ?? 0)
                .Take(take ?? transactionItems.Count)
                .ToList();

            return itemsToInclude;
        }

        private static bool ProcessingLimitExceeded(HashSet<uint256> uniqueProcessedTxIds)
        {
            return uniqueProcessedTxIds.Count >= MaxHistoryItems;
        }
    }
}