using System;
using System.Runtime.CompilerServices;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.Wallet;
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
            var res = new HdAccount
            {
                Name = account.AccountName,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(account.CreationTime),
                ExtendedPubKey = account.ExtPubKey,
                Index = account.AccountIndex,
                HdPath = repo.ToHdPath(account.AccountIndex),
            };

            res.ExternalAddresses = new AddressCollection(res, 0);
            res.InternalAddresses = new AddressCollection(res, 1);

            return res;
        }

        internal static HdAddress ToHdAddress(this SQLiteWalletRepository repo, HDAddress address)
        {
            var pubKeyScript = (address.PubKey == null) ? null : new Script(Encoders.Hex.DecodeData(address.PubKey)); // P2PK
            var scriptPubKey = new Script(Encoders.Hex.DecodeData(address.ScriptPubKey));

            var res = new HdAddress(null)
            {
                Address = address.Address,
                Index = address.AddressIndex,
                AddressType = address.AddressType,
                HdPath = repo.ToHdPath(address.AccountIndex, address.AddressType, address.AddressIndex),
                ScriptPubKey = scriptPubKey,
                Pubkey = pubKeyScript
            };

            return res;
        }

        internal static TransactionData ToTransactionData(this SQLiteWalletRepository repo, HDTransactionData transactionData, TransactionCollection transactionCollection)
        {
            // We need to examine the entire scriptPubKey of the transaction output in question in order to determine if it is a coldstaking output.
            var scriptPubKey = new Script(Encoders.Hex.DecodeData(transactionData.RedeemScript));
            
            // Making the actual cold staking script template available here for checking will be quite messy, so just bring in the relevant check.
            byte[] bytes = scriptPubKey.ToBytes(true);
            bool isColdStaking = ((bytes.Length == 51)
                                  && (bytes[0] == (byte) 0x76) // OP_DUP
                                  && (bytes[1] == (byte) 0xa9) // OP_HASH160
                                  && (bytes[2] == (byte) 0x7b) // OP_ROT
                                  && (bytes[3] == (byte) 0x63) // OP_IF
                                  && (bytes[4] == (byte) 0xb9) // OP_CHECKCOLDSTAKEVERIFY
                                  && (bytes[5] == 0x14)
                                  && (bytes[26] == (byte) 0x67) // OP_ELSE
                                  && (bytes[27] == 0x14)
                                  && (bytes[48] == (byte) 0x68) // OP_ENDIF
                                  && (bytes[49] == (byte) 0x88) // OP_EQUALVERIFY
                                  && (bytes[50] == (byte) 0xac)); // OP_CHECKSIG

            var res = new TransactionData()
            {
                Amount = new Money(transactionData.Value),
                BlockHash = (transactionData.OutputBlockHash == null) ? null : uint256.Parse(transactionData.OutputBlockHash),
                BlockHeight = transactionData.OutputBlockHeight,
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(transactionData.OutputTxTime),
                Id = uint256.Parse(transactionData.OutputTxId),
                Index = transactionData.OutputIndex,
                // These two are always updated and used in tandem so we update them from a single source value.
                IsCoinBase = transactionData.OutputTxIsCoinBase == 1 && transactionData.OutputIndex == 0,
                IsCoinStake = transactionData.OutputTxIsCoinBase == 1 && transactionData.OutputIndex != 0,
                IsColdCoinStake = isColdStaking,
                // IsPropagated  // Not used currently.
                ScriptPubKey = new Script(Encoders.Hex.DecodeData(transactionData.RedeemScript)),
                AddressScriptPubKey = new Script(Encoders.Hex.DecodeData(transactionData.ScriptPubKey)),
                SpendingDetails = (transactionData.SpendTxId == null) ? null : new SpendingDetails()
                {
                    BlockHeight = transactionData.SpendBlockHeight,
                    BlockHash = string.IsNullOrEmpty(transactionData.SpendBlockHash) ? null : uint256.Parse(transactionData.SpendBlockHash),
                    // BlockIndex // Not used currently.
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds((long)transactionData.SpendTxTime),
                    IsCoinStake = transactionData.SpendTxIsCoinBase == 1,
                    TransactionId = uint256.Parse(transactionData.SpendTxId)
                },
                TransactionCollection = transactionCollection
            };

            if (res.SpendingDetails != null)
                res.SpendingDetails.TransactionData = res;

            return res;
        }
    }
}
