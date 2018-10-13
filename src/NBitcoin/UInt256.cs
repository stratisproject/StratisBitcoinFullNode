using System;
using System.Linq;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public class uint256
    {
        public class MutableUint256 : IBitcoinSerializable
        {
            private uint256 _Value;

            public uint256 Value
            {
                get
                {
                    return this._Value;
                }
                set
                {
                    this._Value = value;
                }
            }

            public uint256 MaxValue => uint256.Parse("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

            public MutableUint256()
            {
                this._Value = Zero;
            }
            public MutableUint256(uint256 value)
            {
                this._Value = value;
            }

            public void ReadWrite(BitcoinStream stream)
            {
                if (stream.Serializing)
                {
                    byte[] b = this.Value.ToBytes();
                    stream.ReadWrite(ref b);
                }
                else
                {
                    var b = new byte[WIDTH_BYTE];
                    stream.ReadWrite(ref b);
                    this._Value = new uint256(b);
                }
            }
        }

        private static readonly uint256 _Zero = new uint256();
        public static uint256 Zero
        {
            get { return _Zero; }
        }

        private static readonly uint256 _One = new uint256(1);
        public static uint256 One
        {
            get { return _One; }
        }

        public uint256()
        {
        }

        public uint256(uint256 b)
        {
            this.pn0 = b.pn0;
            this.pn1 = b.pn1;
            this.pn2 = b.pn2;
            this.pn3 = b.pn3;
            this.pn4 = b.pn4;
            this.pn5 = b.pn5;
            this.pn6 = b.pn6;
            this.pn7 = b.pn7;
        }

        private const int WIDTH = 256 / 32;

        private uint256(uint[] array)
        {
            if (array.Length != WIDTH)
                throw new ArgumentOutOfRangeException();

            this.pn0 = array[0];
            this.pn1 = array[1];
            this.pn2 = array[2];
            this.pn3 = array[3];
            this.pn4 = array[4];
            this.pn5 = array[5];
            this.pn6 = array[6];
            this.pn7 = array[7];
        }

        private uint[] ToArray()
        {
            return new uint[] { this.pn0, this.pn1, this.pn2, this.pn3, this.pn4, this.pn5, this.pn6, this.pn7 };
        }

        public static uint256 operator <<(uint256 a, int shift)
        {
            uint[] source = a.ToArray();
            var target = new uint[source.Length];
            int k = shift / 32;
            shift = shift % 32;
            for (int i = 0; i < WIDTH; i++)
            {
                if (i + k + 1 < WIDTH && shift != 0)
                    target[i + k + 1] |= (source[i] >> (32 - shift));
                if (i + k < WIDTH)
                    target[i + k] |= (target[i] << shift);
            }
            return new uint256(target);
        }

        public static uint256 operator >>(uint256 a, int shift)
        {
            uint[] source = a.ToArray();
            var target = new uint[source.Length];
            int k = shift / 32;
            shift = shift % 32;
            for (int i = 0; i < WIDTH; i++)
            {
                if (i - k - 1 >= 0 && shift != 0)
                    target[i - k - 1] |= (source[i] << (32 - shift));
                if (i - k >= 0)
                    target[i - k] |= (source[i] >> shift);
            }
            return new uint256(target);
        }

        public static uint256 Parse(string hex)
        {
            return new uint256(hex);
        }
        public static bool TryParse(string hex, out uint256 result)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            result = null;
            if (hex.Length != WIDTH_BYTE * 2)
                return false;
            if (!((HexEncoder)Encoders.Hex).IsValid(hex))
                return false;
            result = new uint256(hex);
            return true;
        }

        private static readonly HexEncoder Encoder = new HexEncoder();
        private const int WIDTH_BYTE = 256 / 8;
        internal readonly UInt32 pn0;
        internal readonly UInt32 pn1;
        internal readonly UInt32 pn2;
        internal readonly UInt32 pn3;
        internal readonly UInt32 pn4;
        internal readonly UInt32 pn5;
        internal readonly UInt32 pn6;
        internal readonly UInt32 pn7;

        public byte GetByte(int index)
        {
            int uintIndex = index / sizeof(uint);
            int byteIndex = index % sizeof(uint);
            UInt32 value;
            switch (uintIndex)
            {
                case 0:
                    value = this.pn0;
                    break;
                case 1:
                    value = this.pn1;
                    break;
                case 2:
                    value = this.pn2;
                    break;
                case 3:
                    value = this.pn3;
                    break;
                case 4:
                    value = this.pn4;
                    break;
                case 5:
                    value = this.pn5;
                    break;
                case 6:
                    value = this.pn6;
                    break;
                case 7:
                    value = this.pn7;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("index");
            }
            return (byte)(value >> (byteIndex * 8));
        }

        public override string ToString()
        {
            return Encoder.EncodeData(ToBytes().Reverse().ToArray());
        }

        public uint256(ulong b)
        {
            this.pn0 = (uint)b;
            this.pn1 = (uint)(b >> 32);
            this.pn2 = 0;
            this.pn3 = 0;
            this.pn4 = 0;
            this.pn5 = 0;
            this.pn6 = 0;
            this.pn7 = 0;
        }

        public uint256(byte[] vch, bool lendian = true)
        {
            if (vch.Length != WIDTH_BYTE)
            {
                throw new FormatException("the byte array should be 256 byte long");
            }

            if (!lendian)
                vch = vch.Reverse().ToArray();

            this.pn0 = Utils.ToUInt32(vch, 4 * 0, true);
            this.pn1 = Utils.ToUInt32(vch, 4 * 1, true);
            this.pn2 = Utils.ToUInt32(vch, 4 * 2, true);
            this.pn3 = Utils.ToUInt32(vch, 4 * 3, true);
            this.pn4 = Utils.ToUInt32(vch, 4 * 4, true);
            this.pn5 = Utils.ToUInt32(vch, 4 * 5, true);
            this.pn6 = Utils.ToUInt32(vch, 4 * 6, true);
            this.pn7 = Utils.ToUInt32(vch, 4 * 7, true);

        }

        public uint256(string str)
        {
            this.pn0 = 0;
            this.pn1 = 0;
            this.pn2 = 0;
            this.pn3 = 0;
            this.pn4 = 0;
            this.pn5 = 0;
            this.pn6 = 0;
            this.pn7 = 0;
            str = str.Trim();

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                str = str.Substring(2);

            byte[] bytes = Encoder.DecodeData(str).Reverse().ToArray();
            if (bytes.Length != WIDTH_BYTE)
                throw new FormatException("Invalid hex length");
            this.pn0 = Utils.ToUInt32(bytes, 4 * 0, true);
            this.pn1 = Utils.ToUInt32(bytes, 4 * 1, true);
            this.pn2 = Utils.ToUInt32(bytes, 4 * 2, true);
            this.pn3 = Utils.ToUInt32(bytes, 4 * 3, true);
            this.pn4 = Utils.ToUInt32(bytes, 4 * 4, true);
            this.pn5 = Utils.ToUInt32(bytes, 4 * 5, true);
            this.pn6 = Utils.ToUInt32(bytes, 4 * 6, true);
            this.pn7 = Utils.ToUInt32(bytes, 4 * 7, true);

        }

        public uint256(byte[] vch)
            : this(vch, true)
        {
        }

        public override bool Equals(object obj)
        {
            var item = obj as uint256;
            if (item == null)
                return false;
            bool equals = true;
            equals &= this.pn0 == item.pn0;
            equals &= this.pn1 == item.pn1;
            equals &= this.pn2 == item.pn2;
            equals &= this.pn3 == item.pn3;
            equals &= this.pn4 == item.pn4;
            equals &= this.pn5 == item.pn5;
            equals &= this.pn6 == item.pn6;
            equals &= this.pn7 == item.pn7;
            return equals;
        }

        public static bool operator ==(uint256 a, uint256 b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;

            bool equals = true;
            equals &= a.pn0 == b.pn0;
            equals &= a.pn1 == b.pn1;
            equals &= a.pn2 == b.pn2;
            equals &= a.pn3 == b.pn3;
            equals &= a.pn4 == b.pn4;
            equals &= a.pn5 == b.pn5;
            equals &= a.pn6 == b.pn6;
            equals &= a.pn7 == b.pn7;
            return equals;
        }

        public static bool operator <(uint256 a, uint256 b)
        {
            return Comparison(a, b) < 0;
        }

        public static bool operator >(uint256 a, uint256 b)
        {
            return Comparison(a, b) > 0;
        }

        public static bool operator <=(uint256 a, uint256 b)
        {
            return Comparison(a, b) <= 0;
        }

        public static bool operator >=(uint256 a, uint256 b)
        {
            return Comparison(a, b) >= 0;
        }

        public static int Comparison(uint256 a, uint256 b)
        {
            if (a == null)
                throw new ArgumentNullException("a");
            if (b == null)
                throw new ArgumentNullException("b");

            if (a.pn7 < b.pn7)
                return -1;
            if (a.pn7 > b.pn7)
                return 1;
            if (a.pn6 < b.pn6)
                return -1;
            if (a.pn6 > b.pn6)
                return 1;
            if (a.pn5 < b.pn5)
                return -1;
            if (a.pn5 > b.pn5)
                return 1;
            if (a.pn4 < b.pn4)
                return -1;
            if (a.pn4 > b.pn4)
                return 1;
            if (a.pn3 < b.pn3)
                return -1;
            if (a.pn3 > b.pn3)
                return 1;
            if (a.pn2 < b.pn2)
                return -1;
            if (a.pn2 > b.pn2)
                return 1;
            if (a.pn1 < b.pn1)
                return -1;
            if (a.pn1 > b.pn1)
                return 1;
            if (a.pn0 < b.pn0)
                return -1;
            if (a.pn0 > b.pn0)
                return 1;
            return 0;
        }

        public static bool operator !=(uint256 a, uint256 b)
        {
            return !(a == b);
        }

        public static bool operator ==(uint256 a, ulong b)
        {
            return (a == new uint256(b));
        }

        public static bool operator !=(uint256 a, ulong b)
        {
            return !(a == new uint256(b));
        }

        public static implicit operator uint256(ulong value)
        {
            return new uint256(value);
        }


        public byte[] ToBytes(bool lendian = true)
        {
            var arr = new byte[WIDTH_BYTE];
            Buffer.BlockCopy(Utils.ToBytes(this.pn0, true), 0, arr, 4 * 0, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn1, true), 0, arr, 4 * 1, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn2, true), 0, arr, 4 * 2, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn3, true), 0, arr, 4 * 3, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn4, true), 0, arr, 4 * 4, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn5, true), 0, arr, 4 * 5, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn6, true), 0, arr, 4 * 6, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn7, true), 0, arr, 4 * 7, 4);
            if (!lendian)
                Array.Reverse(arr);
            return arr;
        }

        public MutableUint256 AsBitcoinSerializable()
        {
            return new MutableUint256(this);
        }

        public int GetSerializeSize()
        {
            return WIDTH_BYTE;
        }

        public int Size
        {
            get
            {
                return WIDTH_BYTE;
            }
        }

        public ulong GetLow64()
        {
            return this.pn0 | (ulong) this.pn1 << 32;
        }

        public uint GetLow32()
        {
            return this.pn0;
        }

        public override int GetHashCode()
        {
            return (int)this.pn0;
        }
    }

    public class uint160
    {
        public class MutableUint160 : IBitcoinSerializable
        {
            private uint160 _Value;
            public uint160 Value
            {
                get
                {
                    return this._Value;
                }
                set
                {
                    this._Value = value;
                }
            }
            public MutableUint160()
            {
                this._Value = Zero;
            }
            public MutableUint160(uint160 value)
            {
                this._Value = value;
            }

            public void ReadWrite(BitcoinStream stream)
            {
                if (stream.Serializing)
                {
                    byte[] b = this.Value.ToBytes();
                    stream.ReadWrite(ref b);
                }
                else
                {
                    var b = new byte[WIDTH_BYTE];
                    stream.ReadWrite(ref b);
                    this._Value = new uint160(b);
                }
            }
        }

        private static readonly uint160 _Zero = new uint160();
        public static uint160 Zero
        {
            get { return _Zero; }
        }

        private static readonly uint160 _One = new uint160(1);
        public static uint160 One
        {
            get { return _One; }
        }

        public uint160()
        {
        }

        public uint160(uint160 b)
        {
            this.pn0 = b.pn0;
            this.pn1 = b.pn1;
            this.pn2 = b.pn2;
            this.pn3 = b.pn3;
            this.pn4 = b.pn4;
        }

        public static uint160 Parse(string hex)
        {
            return new uint160(hex);
        }
        public static bool TryParse(string hex, out uint160 result)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            result = null;
            if (hex.Length != WIDTH_BYTE * 2)
                return false;
            if (!((HexEncoder)Encoders.Hex).IsValid(hex))
                return false;
            result = new uint160(hex);
            return true;
        }

        private static readonly HexEncoder Encoder = new HexEncoder();
        private const int WIDTH_BYTE = 160 / 8;
        internal readonly UInt32 pn0;
        internal readonly UInt32 pn1;
        internal readonly UInt32 pn2;
        internal readonly UInt32 pn3;
        internal readonly UInt32 pn4;

        public byte GetByte(int index)
        {
            int uintIndex = index / sizeof(uint);
            int byteIndex = index % sizeof(uint);
            UInt32 value;
            switch (uintIndex)
            {
                case 0:
                    value = this.pn0;
                    break;
                case 1:
                    value = this.pn1;
                    break;
                case 2:
                    value = this.pn2;
                    break;
                case 3:
                    value = this.pn3;
                    break;
                case 4:
                    value = this.pn4;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("index");
            }
            return (byte)(value >> (byteIndex * 8));
        }

        public override string ToString()
        {
            return Encoder.EncodeData(ToBytes().Reverse().ToArray());
        }

        public uint160(ulong b)
        {
            this.pn0 = (uint)b;
            this.pn1 = (uint)(b >> 32);
            this.pn2 = 0;
            this.pn3 = 0;
            this.pn4 = 0;
        }

        public uint160(byte[] vch, bool lendian = true)
        {
            if (vch.Length != WIDTH_BYTE)
            {
                throw new FormatException("the byte array should be 160 byte long");
            }

            if (!lendian)
                vch = vch.Reverse().ToArray();

            this.pn0 = Utils.ToUInt32(vch, 4 * 0, true);
            this.pn1 = Utils.ToUInt32(vch, 4 * 1, true);
            this.pn2 = Utils.ToUInt32(vch, 4 * 2, true);
            this.pn3 = Utils.ToUInt32(vch, 4 * 3, true);
            this.pn4 = Utils.ToUInt32(vch, 4 * 4, true);

        }

        public uint160(string str)
        {
            this.pn0 = 0;
            this.pn1 = 0;
            this.pn2 = 0;
            this.pn3 = 0;
            this.pn4 = 0;
            str = str.Trim();

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                str = str.Substring(2);

            byte[] bytes = Encoder.DecodeData(str).Reverse().ToArray();
            if (bytes.Length != WIDTH_BYTE)
                throw new FormatException("Invalid hex length");
            this.pn0 = Utils.ToUInt32(bytes, 4 * 0, true);
            this.pn1 = Utils.ToUInt32(bytes, 4 * 1, true);
            this.pn2 = Utils.ToUInt32(bytes, 4 * 2, true);
            this.pn3 = Utils.ToUInt32(bytes, 4 * 3, true);
            this.pn4 = Utils.ToUInt32(bytes, 4 * 4, true);

        }

        public uint160(byte[] vch)
            : this(vch, true)
        {
        }

        public override bool Equals(object obj)
        {
            var item = obj as uint160;
            if (item == null)
                return false;
            bool equals = true;
            equals &= this.pn0 == item.pn0;
            equals &= this.pn1 == item.pn1;
            equals &= this.pn2 == item.pn2;
            equals &= this.pn3 == item.pn3;
            equals &= this.pn4 == item.pn4;
            return equals;
        }

        public static bool operator ==(uint160 a, uint160 b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;

            bool equals = true;
            equals &= a.pn0 == b.pn0;
            equals &= a.pn1 == b.pn1;
            equals &= a.pn2 == b.pn2;
            equals &= a.pn3 == b.pn3;
            equals &= a.pn4 == b.pn4;
            return equals;
        }

        public static bool operator <(uint160 a, uint160 b)
        {
            return Comparison(a, b) < 0;
        }

        public static bool operator >(uint160 a, uint160 b)
        {
            return Comparison(a, b) > 0;
        }

        public static bool operator <=(uint160 a, uint160 b)
        {
            return Comparison(a, b) <= 0;
        }

        public static bool operator >=(uint160 a, uint160 b)
        {
            return Comparison(a, b) >= 0;
        }

        private static int Comparison(uint160 a, uint160 b)
        {
            if (a.pn4 < b.pn4)
                return -1;
            if (a.pn4 > b.pn4)
                return 1;
            if (a.pn3 < b.pn3)
                return -1;
            if (a.pn3 > b.pn3)
                return 1;
            if (a.pn2 < b.pn2)
                return -1;
            if (a.pn2 > b.pn2)
                return 1;
            if (a.pn1 < b.pn1)
                return -1;
            if (a.pn1 > b.pn1)
                return 1;
            if (a.pn0 < b.pn0)
                return -1;
            if (a.pn0 > b.pn0)
                return 1;
            return 0;
        }

        public static bool operator !=(uint160 a, uint160 b)
        {
            return !(a == b);
        }

        public static bool operator ==(uint160 a, ulong b)
        {
            return (a == new uint160(b));
        }

        public static bool operator !=(uint160 a, ulong b)
        {
            return !(a == new uint160(b));
        }

        public static implicit operator uint160(ulong value)
        {
            return new uint160(value);
        }


        public byte[] ToBytes(bool lendian = true)
        {
            var arr = new byte[WIDTH_BYTE];
            Buffer.BlockCopy(Utils.ToBytes(this.pn0, true), 0, arr, 4 * 0, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn1, true), 0, arr, 4 * 1, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn2, true), 0, arr, 4 * 2, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn3, true), 0, arr, 4 * 3, 4);
            Buffer.BlockCopy(Utils.ToBytes(this.pn4, true), 0, arr, 4 * 4, 4);
            if (!lendian)
                Array.Reverse(arr);
            return arr;
        }

        public MutableUint160 AsBitcoinSerializable()
        {
            return new MutableUint160(this);
        }

        public int GetSerializeSize()
        {
            return WIDTH_BYTE;
        }

        public int Size
        {
            get
            {
                return WIDTH_BYTE;
            }
        }

        public ulong GetLow64()
        {
            return this.pn0 | (ulong) this.pn1 << 32;
        }

        public uint GetLow32()
        {
            return this.pn0;
        }

        public override int GetHashCode()
        {
            return (int)this.pn0;
        }
    }
}