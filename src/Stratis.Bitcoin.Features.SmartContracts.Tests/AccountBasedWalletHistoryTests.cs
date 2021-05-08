using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class AccountBasedWalletHistoryTests
    {
        /// <summary>
        /// Test for spending a single UTXO input to a single output, and receiving change.
        /// </summary>
        [Fact]
        public void WalletHistory_SpendSingleUTXO()
        {
            var query = new AccountBasedWalletHistoryQuery();

            var testAddress = "test";
            var testDestination = "test destination";
            var outputAmount = 9;
            var fee = 1;
            var inputAmount = outputAmount + fee;

            var history = new List<FlatHistory>();

            var flatHistory = new FlatHistory();

            history.Add(flatHistory);

            flatHistory.Address = new HdAddress
            {
                Address = testAddress
            };

            var now = DateTimeOffset.UtcNow;

            // The single input transaction.
            flatHistory.Transaction = new TransactionData
            {
                BlockIndex = 12345,
                BlockHeight = 987654,
                Id = new uint256(1000),

                CreationTime = now.AddMinutes(-1),
                Amount = inputAmount,
                SpendingDetails = new SpendingDetails
                {
                    BlockHeight = 234567,
                    CreationTime = now,
                    TransactionId = new uint256(1),
                    Payments = new List<PaymentDetails>
                    {
                        new PaymentDetails
                        {
                            OutputIndex = 1,
                            Amount = outputAmount,
                            DestinationAddress = testDestination
                        },

                        // The change.
                        new PaymentDetails
                        {
                            OutputIndex = 0,
                            Amount = inputAmount - outputAmount - fee,
                            DestinationAddress = testAddress, // Back to sender
                        }
                    }
                }
            };

            var result = query.GetHistory(history, testAddress);

            Assert.Equal(2, result.Count);

            var sendItem = result[0];
            var receiveItem = result[1];

            Assert.Equal(inputAmount, receiveItem.Amount);
            Assert.Equal(testAddress, receiveItem.ToAddress);
            
            Assert.Equal(TransactionItemType.Received, receiveItem.Type);
            Assert.Equal(flatHistory.Transaction.Id, receiveItem.Id);
            Assert.Equal(flatHistory.Transaction.BlockIndex, receiveItem.BlockIndex);
            Assert.Equal(flatHistory.Transaction.BlockHeight, receiveItem.ConfirmedInBlock);

            Assert.Equal(outputAmount, sendItem.Amount);
            Assert.Equal(fee, sendItem.Fee);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.CreationTime, sendItem.Timestamp);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.BlockIndex, sendItem.BlockIndex);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.BlockHeight, sendItem.ConfirmedInBlock);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.TransactionId, sendItem.Id);
            Assert.Equal(TransactionItemType.Send, sendItem.Type);
            Assert.Single(sendItem.Payments);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.Payments.First().Amount, sendItem.Payments.First().Amount);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.Payments.First().DestinationAddress, sendItem.Payments.First().DestinationAddress);
        }

        [Fact]
        public void WalletHistory_SpendMultipleUTXO()
        {
            var query = new AccountBasedWalletHistoryQuery();

            var testAddress = "test";
            var destinationAddress = "test destination";

            var inputAmount = 7;
            var outputAmount = 9;
            var fee = 1;

            var inputAmount2 = outputAmount - inputAmount + fee + 7;


            var history = new List<FlatHistory>();

            var flatHistory = new FlatHistory();
            var flatHistory2 = new FlatHistory();

            history.Add(flatHistory);
            history.Add(flatHistory2);

            flatHistory.Address = new HdAddress
            {
                Address = testAddress
            };

            flatHistory2.Address = flatHistory.Address;

            var now = DateTimeOffset.UtcNow;

            var spendingDetails = new SpendingDetails // The same spending details as the first UTXO.
            {
                BlockHeight = 234567,
                CreationTime = now,
                TransactionId = new uint256(1), // Same transaction ID as the other transaction data's spending details.
                Payments = new List<PaymentDetails>
                {
                    new PaymentDetails
                    {
                        OutputIndex = 1,
                        Amount = outputAmount,
                        DestinationAddress = destinationAddress // Send to self
                    },
                    // The change.
                    new PaymentDetails
                    {
                        OutputIndex = 0,
                        Amount = inputAmount + inputAmount2 - outputAmount - fee,
                        DestinationAddress = testAddress, // Back to sender
                    }
                }
            };

            // The input transactions.

            // First UTXO being spent.
            flatHistory.Transaction = new TransactionData
            {
                BlockIndex = 12345,
                BlockHeight = 987654,
                Id = new uint256(1000),

                CreationTime = now.AddMinutes(-1),
                Amount = inputAmount,
                SpendingDetails = spendingDetails
            };

            // Second UTXO being spent.
            flatHistory2.Transaction = new TransactionData
            {
                BlockIndex = 8,
                BlockHeight = 22,
                Id = new uint256(2000),

                CreationTime = now.AddMinutes(-2),
                Amount = inputAmount2,
                SpendingDetails = spendingDetails
            };

            var result = query.GetHistory(history, testAddress);

            Assert.Equal(3, result.Count);

            var sendItem = result[0];
            var receiveItem1 = result[1];
            var receiveItem2 = result[2];

            Assert.Equal(inputAmount, receiveItem1.Amount);
            Assert.Equal(TransactionItemType.Received, receiveItem1.Type);
            Assert.Equal(flatHistory.Transaction.Id, receiveItem1.Id);
            Assert.Equal(flatHistory.Transaction.BlockIndex, receiveItem1.BlockIndex);
            Assert.Equal(flatHistory.Transaction.BlockHeight, receiveItem1.ConfirmedInBlock);

            Assert.Equal(inputAmount2, receiveItem2.Amount);
            Assert.Equal(TransactionItemType.Received, receiveItem2.Type);
            Assert.Equal(flatHistory2.Transaction.Id, receiveItem2.Id);
            Assert.Equal(flatHistory2.Transaction.BlockIndex, receiveItem2.BlockIndex);
            Assert.Equal(flatHistory2.Transaction.BlockHeight, receiveItem2.ConfirmedInBlock);

            Assert.Equal(outputAmount, sendItem.Amount);
            Assert.Equal(fee, sendItem.Fee);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.CreationTime, sendItem.Timestamp);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.BlockIndex, sendItem.BlockIndex);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.BlockHeight, sendItem.ConfirmedInBlock);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.TransactionId, sendItem.Id);
            Assert.Equal(TransactionItemType.Send, sendItem.Type);
            Assert.Single(sendItem.Payments);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.Payments.First().Amount, sendItem.Payments.First().Amount);
            Assert.Equal(flatHistory.Transaction.SpendingDetails.Payments.First().DestinationAddress, sendItem.Payments.First().DestinationAddress);
        }

        [Fact]
        public void WalletHistory_SpendMultipleUTXO_To_Self()
        {
            var query = new AccountBasedWalletHistoryQuery();

            var testAddress = "test";
            var outputAmount = 5;
            var fee = 1;
            var inputAmount = 3;
            var inputAmount2 = outputAmount - inputAmount + fee + 5;

            var history = new List<FlatHistory>();

            var firstInputHistoryItem = new FlatHistory();
            var secondInputHistoryItem = new FlatHistory();
            var receiveHistoryItem = new FlatHistory();

            history.Add(firstInputHistoryItem);
            history.Add(secondInputHistoryItem);
            history.Add(receiveHistoryItem);

            firstInputHistoryItem.Address = new HdAddress
            {
                Address = testAddress
            };

            // Same address provides the second input.
            secondInputHistoryItem.Address = firstInputHistoryItem.Address;

            // Same address for the receive.
            receiveHistoryItem.Address = firstInputHistoryItem.Address;

            var now = DateTimeOffset.UtcNow;

            var spendingDetails = new SpendingDetails // The same spending details as the first UTXO.
            {
                CreationTime = now, 
                TransactionId = new uint256(1), // Same transaction ID as the other transaction data's spending details.
                Payments = new List<PaymentDetails>
                {
                    new PaymentDetails
                    {
                        OutputIndex = 1,
                        Amount = outputAmount,
                        DestinationAddress = testAddress // Send to self
                    },
                    // The change.
                    new PaymentDetails
                    {
                        OutputIndex = 0,
                        Amount = inputAmount + inputAmount2 - outputAmount - fee,
                        DestinationAddress = testAddress, // Also send change to self
                    }
                }
            };

            // The input transactions.

            // First UTXO being spent.
            firstInputHistoryItem.Transaction = new TransactionData
            {
                CreationTime = now.AddMinutes(-1),
                Amount = inputAmount,
                SpendingDetails = spendingDetails
            };

            // Second UTXO being spent.
            secondInputHistoryItem.Transaction = new TransactionData
            {
                CreationTime = now.AddMinutes(-2),
                Amount = inputAmount2,
                SpendingDetails = spendingDetails
            };

            // Add another transaction for the receive.
            receiveHistoryItem.Transaction = new TransactionData
            {
                CreationTime = now.AddMinutes(1),
                Amount = outputAmount
            };

            var result = query.GetHistory(history, testAddress);

            Assert.Equal(4, result.Count);

            // These should be in the correct order.
            var receive = result[0];
            var send = result[1];
            var firstInput = result[2];
            var secondInput = result[3];

            Assert.Equal(outputAmount, receive.Amount);
            Assert.Equal(outputAmount, send.Amount);
            Assert.Equal(fee, send.Fee);
            Assert.Equal(inputAmount, firstInput.Amount);
            Assert.Equal(inputAmount2, secondInput.Amount);
        }

        /// <summary>
        /// Ensures that a TransactionData item marked as change still has its payments
        /// added to the history.
        /// </summary>
        [Fact]
        public void WalletHistory_Transaction_Spent_After_New_Transactions()
        {
            var query = new AccountBasedWalletHistoryQuery();

            var testAddress = "CUtNvY1Jxpn4V4RD1tgphsUKpQdo4q5i54";

            var history = new List<FlatHistory>();

            var firstInputHistoryItem = new FlatHistory();
            var secondInputHistoryItem = new FlatHistory();
            var thirdInputHistoryItem = new FlatHistory();
            var fourthInputHistoryItem = new FlatHistory();

            history.Add(firstInputHistoryItem);
            history.Add(secondInputHistoryItem);
            history.Add(thirdInputHistoryItem);
            history.Add(fourthInputHistoryItem);

            firstInputHistoryItem.Address = new HdAddress
            {
                Address = testAddress
            };

            secondInputHistoryItem.Address = firstInputHistoryItem.Address;
            thirdInputHistoryItem.Address = firstInputHistoryItem.Address;
            fourthInputHistoryItem.Address = firstInputHistoryItem.Address;

            firstInputHistoryItem.Transaction = new TransactionData
            {
                Id = new uint256("e74c1de67a8d861dc10088bde97d0c9a416583342375c20de3e5f0ea82d33978"),
                CreationTime = Utils.UnixTimeToDateTime(1572525168),
                Amount = 10000000000000000,
                Index = 0,
                BlockHeight =  2,
                BlockIndex = 1,
                IsCoinBase = true,
                SpendingDetails = new SpendingDetails
                {
                    BlockHeight = 5,
                    CreationTime = Utils.UnixTimeToDateTime(1572525216),
                    TransactionId = new uint256("1d14257b77c543077d1178e46ddbae963a34e1bcb1c6359f8f6967768a9aad3b"),
                    Payments = new List<PaymentDetails>
                    {
                        new PaymentDetails
                        {
                            OutputIndex = 0,
                            Amount = 9999998999990000,
                            DestinationAddress = testAddress // Send to self
                        },
                        new PaymentDetails
                        {
                            OutputIndex = 1,
                            Amount = 1000000000,
                            DestinationAddress = testAddress, // Also send change to self
                        }
                    }
                }
            };

            secondInputHistoryItem.Transaction = new TransactionData
            {
                Id = new uint256("1d14257b77c543077d1178e46ddbae963a34e1bcb1c6359f8f6967768a9aad3b"),
                CreationTime = Utils.UnixTimeToDateTime(1572525216),
                Amount = 9999998999990000,
                Index = 0,
                BlockHeight = 5,
                BlockIndex = 1,
                SpendingDetails = new SpendingDetails
                {
                    BlockHeight = 55,
                    CreationTime = Utils.UnixTimeToDateTime(1572526192),
                    TransactionId = new uint256("a14b74411bb1850e2ea0f4026916d8d1b64f01290317fcbc00966e73d31e56c5"),
                    Payments = new List<PaymentDetails>
                    {
                        new PaymentDetails
                        {
                            OutputIndex = 0,
                            Amount = 9999996999980000,
                            DestinationAddress = testAddress // Send to self
                        },
                        new PaymentDetails
                        {
                            OutputIndex = 1,
                            Amount = 2000000000,
                            DestinationAddress = testAddress, // Also send change to self
                        }
                    }
                }
            };

            thirdInputHistoryItem.Transaction = new TransactionData
            {
                Id = new uint256("1d14257b77c543077d1178e46ddbae963a34e1bcb1c6359f8f6967768a9aad3b"),
                CreationTime = Utils.UnixTimeToDateTime(1572525216),
                Amount = 1000000000,
                Index = 0,
                BlockHeight = 5,
                BlockIndex = 1,
                SpendingDetails = new SpendingDetails
                {
                    BlockHeight = 57,
                    CreationTime = Utils.UnixTimeToDateTime(1572526224),
                    TransactionId = new uint256("786b12b1726146dd8b07b1717f2180aca8d5e52d467ed515c54f66877d64b288"),
                    Payments = new List<PaymentDetails>
                    {
                        new PaymentDetails
                        {
                            OutputIndex = 0,
                            Amount = 899990000,
                            DestinationAddress = testAddress // Send to self
                        },
                        new PaymentDetails
                        {
                            OutputIndex = 1,
                            Amount = 100000000,
                            DestinationAddress = testAddress, // Also send change to self
                        }
                    }
                }
            };

            fourthInputHistoryItem.Transaction = new TransactionData
            {
                Id = new uint256("a14b74411bb1850e2ea0f4026916d8d1b64f01290317fcbc00966e73d31e56c5"),
                CreationTime = Utils.UnixTimeToDateTime(1572526192),
                Amount = 9999996999980000,
                Index = 0,
                BlockHeight = 55,
                BlockIndex = 1,
                SpendingDetails = new SpendingDetails
                {
                    BlockHeight = 59,
                    CreationTime = Utils.UnixTimeToDateTime(1572526256),
                    TransactionId = new uint256("d52d91d272e3f8e9068fec781c3759182549aecae53cdd18d3ab1d5b102641b7"),
                    Payments = new List<PaymentDetails>
                    {
                        new PaymentDetails
                        {
                            OutputIndex = 0,
                            Amount = 9999993999970000,
                            DestinationAddress = testAddress // Send to self
                        },
                        new PaymentDetails
                        {
                            OutputIndex = 1,
                            Amount = 3000000000,
                            DestinationAddress = testAddress, // Also send change to self
                        }
                    }
                }
            };

            var result = query.GetHistory(history, testAddress);

            Assert.Equal(5, result.Count);

            var first = result[0];
            var second = result[1];
            var third = result[2];
            var fourth = result[3];
            var fifth = result[4];

            Assert.Equal((Money)10000000000000000, fifth.Amount);
            Assert.Equal((Money)1000000000, fourth.Amount);
            Assert.Equal(2000000000, third.Amount);
            Assert.Equal(100000000, second.Amount);
            Assert.Equal(3000000000, first.Amount);
        }
    }
}