using NBitcoin;

namespace LedgerWallet
{
    class BufferUtils
    {
        internal static void WriteUint32BE(System.IO.Stream data, long index)
        {
            var bytes = Utils.ToBytes((uint)index, false);
            data.Write(bytes, 0, bytes.Length);
        }

        internal static void WriteBuffer(System.IO.Stream data, byte[] buffer)
        {
            data.Write(buffer, 0, buffer.Length);
        }

        internal static void WriteBuffer(System.IO.Stream data, IBitcoinSerializable serializable)
        {
            WriteBuffer(data, serializable.ToBytes());
        }

        internal static void WriteBuffer(System.IO.Stream data, uint value)
        {
            var bytes = Utils.ToBytes(value, true);
            data.Write(bytes, 0, bytes.Length);
        }
    }
}
