using NBitcoin.DataEncoders;
using System;
using System.Linq;

namespace LedgerWallet
{
    public class Bip32EncodedKey
    {
        readonly byte[] _Key;
        public Bip32EncodedKey(byte[] bytes)
        {
            if(bytes == null)
                throw new ArgumentNullException("bytes");
            _Key = bytes.ToArray();
        }

        public byte[] ToBytes()
        {
            return _Key.ToArray();
        }

        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_Key);
        }
    }
}
