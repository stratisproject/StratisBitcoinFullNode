using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NBitcoin
{
    internal class BitReader
    {
        private BitArray array;
        public BitReader(byte[] data, int bitCount)
        {
            var writer = new BitWriter();
            writer.Write(data, bitCount);
            this.array = writer.ToBitArray();
        }

        public BitReader(BitArray array)
        {
            this.array = new BitArray(array.Length);
            for(int i = 0; i < array.Length; i++)
                this.array.Set(i, array.Get(i));
        }

        public bool Read()
        {
            bool v = this.array.Get(this.Position);
            this.Position++;
            return v;
        }

        public int Position
        {
            get;
            set;
        }

        public uint ReadUInt(int bitCount)
        {
            uint value = 0;
            for(int i = 0; i < bitCount; i++)
            {
                uint v = Read() ? 1U : 0U;
                value += (v << i);
            }
            return value;
        }

        public int Count
        {
            get
            {
                return this.array.Length;
            }
        }

        public BitArray ToBitArray()
        {
            var result = new BitArray(this.array.Length);
            for(int i = 0; i < this.array.Length; i++)
                result.Set(i, this.array.Get(i));
            return result;
        }

        public BitWriter ToWriter()
        {
            var writer = new BitWriter();
            writer.Write(this.array);
            return writer;
        }

        public void Consume(int count)
        {
            this.Position += count;
        }

        public bool Same(BitReader b)
        {
            while(this.Position != this.Count && b.Position != b.Count)
            {
                bool valuea = Read();
                bool valueb = b.Read();
                if(valuea != valueb)
                    return false;
            }
            return true;
        }

        public override string ToString()
        {
            var builder = new StringBuilder(this.array.Length);
            for(int i = 0; i < this.Count; i++)
            {
                if(i != 0 && i % 8 == 0)
                    builder.Append(' ');
                builder.Append(this.array.Get(i) ? "1" : "0");
            }
            return builder.ToString();
        }
    }

    internal class BitWriter
    {
        private List<bool> values = new List<bool>();
        public int Count
        {
            get
            {
                return this.values.Count;
            }
        }
        public void Write(bool value)
        {
            this.values.Insert(this.Position, value);
            this._Position++;
        }

        internal void Write(byte[] bytes)
        {
            Write(bytes, bytes.Length * 8);
        }

        public void Write(byte[] bytes, int bitCount)
        {
            bytes = SwapEndianBytes(bytes);
            var array = new BitArray(bytes);
            this.values.InsertRange(this.Position, array.OfType<bool>().Take(bitCount));
            this._Position += bitCount;
        }

        public byte[] ToBytes()
        {
            BitArray array = ToBitArray();
            byte[] bytes = ToByteArray(array);
            bytes = SwapEndianBytes(bytes);
            return bytes;
        }

        //BitArray.CopyTo do not exist in portable lib
        private static byte[] ToByteArray(BitArray bits)
        {
            int arrayLength = bits.Length / 8;
            if(bits.Length % 8 != 0)
                arrayLength++;
            var array = new byte[arrayLength];

            for(int i = 0; i < bits.Length; i++)
            {
                int b = i / 8;
                int offset = i % 8;
                array[b] |= bits.Get(i) ? (byte)(1 << offset) : (byte)0;
            }
            return array;
        }


        public BitArray ToBitArray()
        {
            return new BitArray(this.values.ToArray());
        }

        public int[] ToIntegers()
        {
            var array = new BitArray(this.values.ToArray());
            return Wordlist.ToIntegers(array);
        }


        private static byte[] SwapEndianBytes(byte[] bytes)
        {
            var output = new byte[bytes.Length];
            for(int i = 0; i < output.Length; i++)
            {
                byte newByte = 0;
                for(int ib = 0; ib < 8; ib++)
                {
                    newByte += (byte)(((bytes[i] >> ib) & 1) << (7 - ib));
                }
                output[i] = newByte;
            }
            return output;
        }



        public void Write(uint value, int bitCount)
        {
            for(int i = 0; i < bitCount; i++)
            {
                Write((value & 1) == 1);
                value = value >> 1;
            }
        }

        private int _Position;
        public int Position
        {
            get
            {
                return this._Position;
            }
            set
            {
                this._Position = value;
            }
        }

        internal void Write(BitReader reader, int bitCount)
        {
            for(int i = 0; i < bitCount; i++)
            {
                Write(reader.Read());
            }
        }

        public void Write(BitArray bitArray)
        {
            Write(bitArray, bitArray.Length);
        }
        public void Write(BitArray bitArray, int bitCount)
        {
            for(int i = 0; i < bitCount; i++)
            {
                Write(bitArray.Get(i));
            }
        }

        public void Write(BitReader reader)
        {
            Write(reader, reader.Count - reader.Position);
        }

        public BitReader ToReader()
        {
            return new BitReader(ToBitArray());
        }

        public override string ToString()
        {
            var builder = new StringBuilder(this.values.Count);
            for(int i = 0; i < this.Count; i++)
            {
                if(i != 0 && i % 8 == 0)
                    builder.Append(' ');
                builder.Append(this.values[i] ? "1" : "0");
            }
            return builder.ToString();
        }
    }

}
