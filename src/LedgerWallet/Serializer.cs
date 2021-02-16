using NBitcoin;
using System.IO;

namespace LedgerWallet
{
    class Serializer
    {
        public static byte[] Serialize(KeyPath keyPath)
        {
            Guard.AssertKeyPath(keyPath);
            var ms = new MemoryStream();
            ms.WriteByte((byte)keyPath.Indexes.Length);
            for(var i = 0; i < keyPath.Indexes.Length; i++)
            {
                var bytes = ToBytes(keyPath.Indexes[i], false);
                ms.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }
        internal static byte[] ToBytes(uint value, bool littleEndian)
        {
            if(littleEndian)
            {
                return new byte[]
                {
                    (byte)value,
                    (byte)(value >> 8),
                    (byte)(value >> 16),
                    (byte)(value >> 24),
                };
            }
            else
            {
                return new byte[]
                {
                    (byte)(value >> 24),
                    (byte)(value >> 16),
                    (byte)(value >> 8),
                    (byte)value,
                };
            }
        }
    }
}
