using Stratis.Bitcoin.Features.Wallet.Models;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Extensions
{
    public static class MappingExtensions
    {
        public static TransactionItemModel ToTransactionItemModel(this TransactionData transaction,
            TransactionItemType transactionItemType, string toAddress, Money amount)
        {
            return new TransactionItemModel
            {
                Type = transactionItemType,
                ToAddress = toAddress,
                Amount = amount,
                Id = transaction.SpendingDetails.TransactionId,
                Timestamp = transaction.SpendingDetails.CreationTime,
                TxOutputIndex = transaction.Index,
                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                BlockIndex = transaction.SpendingDetails.BlockIndex
            };
        }
    }
}