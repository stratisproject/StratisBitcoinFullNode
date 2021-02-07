using NBitcoin.DataEncoders;
using System;
using System.Linq;

namespace LedgerWallet
{
    public class LedgerEncodedKey
    {
        readonly byte[] _Key;
        public LedgerEncodedKey(byte[] key)
        {
            if(key == null)
                throw new ArgumentNullException("key");
            _Key = key.ToArray();
        }

        public byte[] ToBytes()
        {
            return _Key.ToArray();
        }

        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_Key, 0, _Key.Length);
        }
    }
}
