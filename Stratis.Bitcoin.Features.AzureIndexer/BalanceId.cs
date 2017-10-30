using NBitcoin.Indexer.DamienG.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public enum BalanceType
    {
        Wallet,
        Address
    }
    public class BalanceId
    {
        const string WalletPrefix = "w$";
        const string HashPrefix = "h$";

        internal const int MaxScriptSize = 512;
        public BalanceId(string walletId)
        {
            _Internal = WalletPrefix + FastEncoder.Instance.EncodeData(Encoding.UTF8.GetBytes(walletId));
        }
        public BalanceId(Script scriptPubKey)
        {
            var pubKey = scriptPubKey.ToBytes(true);
            if (pubKey.Length > MaxScriptSize)
                _Internal = HashPrefix + FastEncoder.Instance.EncodeData(scriptPubKey.Hash.ToBytes(true));
            else
                _Internal = FastEncoder.Instance.EncodeData(scriptPubKey.ToBytes(true));
        }
        public BalanceId(IDestination destination)
            : this(destination.ScriptPubKey)
        {
        }

        private BalanceId()
        {

        }

        public string GetWalletId()
        {
            if (!_Internal.StartsWith(WalletPrefix))
                throw new InvalidOperationException("This balance id does not represent a wallet");
            return Encoding.UTF8.GetString(FastEncoder.Instance.DecodeData(_Internal.Substring(WalletPrefix.Length)));
        }

        public BalanceType Type
        {
            get
            {
                return _Internal.StartsWith(WalletPrefix) ? BalanceType.Wallet : BalanceType.Address;
            }
        }

        public string PartitionKey
        {
            get
            {
                if (_PartitionKey == null)
                {
                    _PartitionKey = Helper.GetPartitionKey(10, Crc32.Compute(_Internal));
                }
                return _PartitionKey;
            }
        }

        public Script ExtractScript()
        {
            if (!ContainsScript)
                return null;
            return Script.FromBytesUnsafe(FastEncoder.Instance.DecodeData(_Internal));
        }

        public bool ContainsScript
        {
            get
            {
                return _Internal.Length >= 2 && _Internal[1] != '$';
            }
        }


        string _PartitionKey;
        string _Internal;
        public override string ToString()
        {
            return _Internal;
        }

        public static BalanceId Parse(string balanceId)
        {
            return new BalanceId()
            {
                _Internal = balanceId
            };
        }

        
    }
}
