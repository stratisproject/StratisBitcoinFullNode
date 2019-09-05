using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.External;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// This class converts internal types to external types for passing back via the public repository methods.
    /// </summary>
    internal static class SQLiteWalletRepositoryExt
    {
        internal static string ToHdPath(this SQLiteWalletRepository repo, int accountIndex)
        {
            return $"m/44'/{repo.Network.Consensus.CoinType}'/{accountIndex}'";
        }

        internal static string ToHdPath(this SQLiteWalletRepository repo, int accountIndex, int addressType, int addressIndex)
        {
            return $"m/44'/{repo.Network.Consensus.CoinType}'/{accountIndex}'/{addressType}/{addressIndex}";
        }

        internal static HdAccount ToHdAccount(this SQLiteWalletRepository repo, HDAccount account)
        {
            return new HdAccount()
            {
                Name = account.AccountName,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(account.CreationTime),
                ExtendedPubKey = account.ExtPubKey,
                Index = account.AccountIndex,
                HdPath = repo.ToHdPath(account.AccountIndex)
            };
        }

        internal static HdAddress ToHdAddress(this SQLiteWalletRepository repo, HDAddress address)
        {
            var pubKeyScript = new Script(Encoders.Hex.DecodeData(address.PubKey)); // P2PK
            var scriptPubKey = new Script(Encoders.Hex.DecodeData(address.ScriptPubKey));

            var res = new HdAddress()
            {
                Address = repo.ScriptAddressReader.GetAddressFromScriptPubKey(repo.Network, scriptPubKey),
                Index = address.AddressIndex,
                HdPath = repo.ToHdPath(address.AccountIndex, address.AddressType, address.AddressIndex),
                ScriptPubKey = new Script(Encoders.Hex.DecodeData(address.ScriptPubKey)),
                Pubkey = pubKeyScript
            };

            return res;
        }

        internal static TransactionData ToTransactionData(this SQLiteWalletRepository repo, HDTransactionData transactionData, IEnumerable<HDPayment> payments)
        {
            return new TransactionData()
            {
                Amount = new Money(transactionData.Value, MoneyUnit.BTC),
                BlockHash = uint256.Parse(transactionData.OutputBlockHash),
                BlockHeight = transactionData.OutputBlockHeight,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(transactionData.OutputTxTime),
                Id = uint256.Parse(transactionData.OutputTxId),
                Index = transactionData.OutputIndex,
                // These two are always updated and used in tandem so we update them from a single source value.
                IsCoinBase = transactionData.OutputTxIsCoinBase == 1,
                IsCoinStake = transactionData.OutputTxIsCoinBase == 1,
                // IsPropagated  // Not used currently.
                ScriptPubKey = new Script(Encoders.Hex.DecodeData(transactionData.RedeemScript)),
                SpendingDetails = (transactionData.SpendTxId == null) ? null : new SpendingDetails()
                {
                    BlockHeight = transactionData.SpendBlockHeight,
                    // BlockIndex // Not used currently.
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds((int)transactionData.SpendTxTime),
                    IsCoinStake = transactionData.SpendTxIsCoinBase == 1,
                    TransactionId = uint256.Parse(transactionData.SpendTxId),
                    Payments = payments.Select(p => new PaymentDetails()
                    {
                         Amount = new Money((decimal)p.SpendValue, MoneyUnit.BTC),
                         DestinationScriptPubKey = new Script(Encoders.Hex.DecodeData(p.SpendScriptPubKey)),
                         OutputIndex = p.SpendIndex
                    }).ToList()
                }
            };
        }
    }
}
