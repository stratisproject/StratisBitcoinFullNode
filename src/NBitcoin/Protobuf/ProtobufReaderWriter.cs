using System;
using System.IO;
using System.Text;

namespace NBitcoin.Protobuf
{
    internal class ProtobufReaderWriter
    {
        internal const int PROTOBUF_VARINT = 0; // int32, int64, uint32, uint64, sint32, sint64, bool, enum
        internal const int PROTOBUF_64BIT = 1; // fixed64, sfixed64, double
        internal const int PROTOBUF_LENDELIM = 2; // string, bytes, embedded messages, packed repeated fields
        internal const int PROTOBUF_32BIT = 5; // fixed32, sfixed32, float

        private Stream _Inner;
        public Stream Inner
        {
            get
            {
                return this._Inner;
            }
        }
        public ProtobufReaderWriter(Stream stream)
        {
            this._Inner = stream;
        }

        public ulong ReadULong()
        {
            ulong v = 0;
            TryReadULong(out v);
            return v;
        }

        public bool TryReadULong(out ulong value)
        {
            value = 0;
            ulong varInt = 0;
            byte b = 0x80;
            int i = 0;
            while((b & 0x80) != 0)
            {
                int v = this._Inner.ReadByte();
                if(v < 0)
                    return false;
                b = (byte)v;
                this.Position++;
                varInt += (ulong)(b & 0x7f) << 7 * i++;
            }
            value = ((b & 0x80) != 0) ? 0 : varInt;
            return true;
        }

        public void WriteULong(ulong value)
        {
            var ioBuffer = new byte[10];
            int ioIndex = 0;
            int count = 0;
            do
            {
                ioBuffer[ioIndex++] = (byte)((value & 0x7F) | 0x80);
                count++;
            } while((value >>= 7) != 0);
            ioBuffer[ioIndex - 1] &= 0x7F;
            this._Inner.Write(ioBuffer, 0, ioIndex);
            this.Position += ioIndex;
        }

        private Encoding Encoding = Encoding.UTF8;

        public void WriteKey(int key, int type)
        {
            ulong v = (ulong)((key << 3) | type);
            WriteULong(v);
        }

        public bool TryReadKey(out int key)
        {
            key = 0;
            ulong lkey;
            if(!TryReadULong(out lkey))
                return false;
            key = (int)lkey;
            int dataType = (int)(key & 0x07);
            key = (int)(key >> 3);
            return true;
        }

        public string ReadString()
        {
            ulong len = ReadULong();
            AssertBounds(len);
            var ioBuffer = new byte[(int)len];
            this._Inner.Read(ioBuffer, 0, ioBuffer.Length);
            this.Position += ioBuffer.Length;
            return this.Encoding.GetString(ioBuffer, 0, ioBuffer.Length);
        }

        public void WriteString(string value)
        {
            int predicted = this.Encoding.GetByteCount(value);
            WriteULong((ulong)predicted);
            AssertBounds((ulong)predicted);
            var ioBuffer = new byte[predicted];
            this.Encoding.GetBytes(value, 0, predicted, ioBuffer, 0);
            this._Inner.Write(ioBuffer, 0, predicted);
            this.Position += predicted;
        }

        public byte[] ReadBytes()
        {
            ulong len = ReadULong();
            AssertBounds(len);
            var ioBuffer = new byte[(int)len];
            this._Inner.Read(ioBuffer, 0, ioBuffer.Length);
            this.Position += ioBuffer.Length;
            return ioBuffer;
        }

        private void AssertBounds(ulong len)
        {
            if((int)len > MaxLength)
                throw new ArgumentOutOfRangeException("The deserialized message is too big");
        }

        public void WriteBytes(byte[] value)
        {
            WriteULong((ulong)value.Length);
            this._Inner.Write(value, 0, value.Length);
            this.Position += value.Length;
        }

        private int _Position;
        public int Position
        {
            get
            {
                return this._Position;
            }
            private set
            {
                this._Position = value;
                if(this.Position > MaxLength)
                    throw new ArgumentOutOfRangeException("The deserialized message is too big");
            }
        }

        private const int MaxLength = 60000;
    }
}
