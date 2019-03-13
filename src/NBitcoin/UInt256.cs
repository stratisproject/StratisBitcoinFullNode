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

            public static uint256 MaxValue => uint256.Parse("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

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
            this.pn = new uint[8];
        }

        public uint256(uint256 b) : this()
        {
            Buffer.BlockCopy(b.pn, 0, this.pn, 0, this.pn.Length);
        }

        private const int WIDTH = 256 / 32;

        private uint256(uint[] array)
        {
            if (array.Length != WIDTH)
                throw new ArgumentOutOfRangeException();

            //No need to call parameter-less constractor as we assign array from argument
            pn = array;
        }

        private uint[] ToArray()
        {
            return this.pn;
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
            if (hex.Length > WIDTH_BYTE * 2 && hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
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
        internal readonly UInt32[] pn;

        public byte GetByte(int index)
        {
            int uintIndex = index / sizeof(uint);
            int byteIndex = index % sizeof(uint);
            UInt32 value;

            if (uintIndex < 0 || uintIndex > 7)
                throw new ArgumentOutOfRangeException("index");

            value = this.pn[uintIndex];
            return (byte)(value >> (byteIndex * 8));
        }

        public override string ToString()
        {
            return Encoder.EncodeData(ToBytes(false));
        }

        public uint256(ulong b) : this()
        {
            //No need to set other elements as thay are set to 0 by default
            this.pn[0] = (uint)b;
            this.pn[1] = (uint)(b >> 32);
        }

        public uint256(byte[] vch, bool lendian = true) : this()
        {
            if (vch.Length != WIDTH_BYTE)
            {
                throw new FormatException("the byte array should be 256 byte long");
            }

            if (lendian)
            {
                this.pn[0] = Utils.ToUInt32(vch, 4 * 0, true);
                this.pn[1] = Utils.ToUInt32(vch, 4 * 1, true);
                this.pn[2] = Utils.ToUInt32(vch, 4 * 2, true);
                this.pn[3] = Utils.ToUInt32(vch, 4 * 3, true);
                this.pn[4] = Utils.ToUInt32(vch, 4 * 4, true);
                this.pn[5] = Utils.ToUInt32(vch, 4 * 5, true);
                this.pn[6] = Utils.ToUInt32(vch, 4 * 6, true);
                this.pn[7] = Utils.ToUInt32(vch, 4 * 7, true);
            }
            else
            {
                this.pn[7] = Utils.ToUInt32(vch, 4 * 0, false);
                this.pn[6] = Utils.ToUInt32(vch, 4 * 1, false);
                this.pn[5] = Utils.ToUInt32(vch, 4 * 2, false);
                this.pn[4] = Utils.ToUInt32(vch, 4 * 3, false);
                this.pn[3] = Utils.ToUInt32(vch, 4 * 4, false);
                this.pn[2] = Utils.ToUInt32(vch, 4 * 5, false);
                this.pn[1] = Utils.ToUInt32(vch, 4 * 6, false);
                this.pn[0] = Utils.ToUInt32(vch, 4 * 7, false);
            }
        }

        public uint256(string str) : this()
        {
            if (str.Length > WIDTH_BYTE * 2)
                str = str.Trim();
            if (str.Length > WIDTH_BYTE * 2 && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                str = str.Substring(2);

            byte[] bytes = Encoder.DecodeData(str);
            if (bytes.Length != WIDTH_BYTE)
                throw new FormatException("Invalid hex length");

            //Read bytes as littleEndian
            this.pn[7] = Utils.ToUInt32(bytes, 4 * 0, false);
            this.pn[6] = Utils.ToUInt32(bytes, 4 * 1, false);
            this.pn[5] = Utils.ToUInt32(bytes, 4 * 2, false);
            this.pn[4] = Utils.ToUInt32(bytes, 4 * 3, false);
            this.pn[3] = Utils.ToUInt32(bytes, 4 * 4, false);
            this.pn[2] = Utils.ToUInt32(bytes, 4 * 5, false);
            this.pn[1] = Utils.ToUInt32(bytes, 4 * 6, false);
            this.pn[0] = Utils.ToUInt32(bytes, 4 * 7, false);
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
            equals &= this.pn[0] == item.pn[0];
            equals &= this.pn[1] == item.pn[1];
            equals &= this.pn[2] == item.pn[2];
            equals &= this.pn[3] == item.pn[3];
            equals &= this.pn[4] == item.pn[4];
            equals &= this.pn[5] == item.pn[5];
            equals &= this.pn[6] == item.pn[6];
            equals &= this.pn[7] == item.pn[7];
            return equals;
        }

        public static bool operator ==(uint256 a, uint256 b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;

            bool equals = true;
            equals &= a.pn[0] == b.pn[0];
            equals &= a.pn[1] == b.pn[1];
            equals &= a.pn[2] == b.pn[2];
            equals &= a.pn[3] == b.pn[3];
            equals &= a.pn[4] == b.pn[4];
            equals &= a.pn[5] == b.pn[5];
            equals &= a.pn[6] == b.pn[6];
            equals &= a.pn[7] == b.pn[7];
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

            if (a.pn[7] < b.pn[7])
                return -1;
            if (a.pn[7] > b.pn[7])
                return 1;
            if (a.pn[6] < b.pn[6])
                return -1;
            if (a.pn[6] > b.pn[6])
                return 1;
            if (a.pn[5] < b.pn[5])
                return -1;
            if (a.pn[5] > b.pn[5])
                return 1;
            if (a.pn[4] < b.pn[4])
                return -1;
            if (a.pn[4] > b.pn[4])
                return 1;
            if (a.pn[3] < b.pn[3])
                return -1;
            if (a.pn[3] > b.pn[3])
                return 1;
            if (a.pn[2] < b.pn[2])
                return -1;
            if (a.pn[2] > b.pn[2])
                return 1;
            if (a.pn[1] < b.pn[1])
                return -1;
            if (a.pn[1] > b.pn[1])
                return 1;
            if (a.pn[0] < b.pn[0])
                return -1;
            if (a.pn[0] > b.pn[0])
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

            if (lendian)
            {
                Buffer.BlockCopy(Utils.ToBytes(this.pn[0], true), 0, arr, 4 * 0, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[1], true), 0, arr, 4 * 1, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[2], true), 0, arr, 4 * 2, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[3], true), 0, arr, 4 * 3, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[4], true), 0, arr, 4 * 4, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[5], true), 0, arr, 4 * 5, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[6], true), 0, arr, 4 * 6, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[7], true), 0, arr, 4 * 7, 4);
            }
            else
            {
                Buffer.BlockCopy(Utils.ToBytes(this.pn[7], false), 0, arr, 4 * 0, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[6], false), 0, arr, 4 * 1, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[5], false), 0, arr, 4 * 2, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[4], false), 0, arr, 4 * 3, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[3], false), 0, arr, 4 * 4, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[2], false), 0, arr, 4 * 5, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[1], false), 0, arr, 4 * 6, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[0], false), 0, arr, 4 * 7, 4);
            }
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
            return this.pn[0] | (ulong) this.pn[1] << 32;
        }

        public uint GetLow32()
        {
            return this.pn[0];
        }

        public override int GetHashCode()
        {
            //Since we are storing data in the array in big-endian format the first few bytes might be zero for PoW.
            //In order to reduce hash collision it is prefered to return last byte as a hash
            return (int)this.pn[7];
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
            this.pn = new uint[5];
        }

        public uint160(uint160 b) : this()
        {
            Buffer.BlockCopy(b.pn, 0, this.pn, 0, this.pn.Length);
        }

        public static uint160 Parse(string hex)
        {
            return new uint160(hex);
        }
        public static bool TryParse(string hex, out uint160 result)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            if (hex.Length > WIDTH_BYTE * 2 && hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
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
        internal readonly UInt32[] pn;

        public byte GetByte(int index)
        {
            int uintIndex = index / sizeof(uint);
            int byteIndex = index % sizeof(uint);
            UInt32 value;

            if (uintIndex < 0 || uintIndex > 7)
                throw new ArgumentOutOfRangeException("index");

            value = this.pn[uintIndex];
            return (byte)(value >> (byteIndex * 8));
        }

        public override string ToString()
        {
            return Encoder.EncodeData(ToBytes(false));
        }

        public uint160(ulong b) : this()
        {
            //No need to set other elements as thay are set to 0 by default
            this.pn[0] = (uint)b;
            this.pn[1] = (uint)(b >> 32);
        }

        public uint160(byte[] vch, bool lendian = true) : this()
        {
            if (vch.Length != WIDTH_BYTE)
            {
                throw new FormatException("the byte array should be 160 byte long");
            }

            if (lendian)
            {
                this.pn[0] = Utils.ToUInt32(vch, 4 * 0, true);
                this.pn[1] = Utils.ToUInt32(vch, 4 * 1, true);
                this.pn[2] = Utils.ToUInt32(vch, 4 * 2, true);
                this.pn[3] = Utils.ToUInt32(vch, 4 * 3, true);
                this.pn[4] = Utils.ToUInt32(vch, 4 * 4, true);
            }
            else
            {
                this.pn[4] = Utils.ToUInt32(vch, 4 * 0, false);
                this.pn[3] = Utils.ToUInt32(vch, 4 * 1, false);
                this.pn[2] = Utils.ToUInt32(vch, 4 * 2, false);
                this.pn[1] = Utils.ToUInt32(vch, 4 * 3, false);
                this.pn[0] = Utils.ToUInt32(vch, 4 * 4, false);
            }
        }

        public uint160(string str) : this()
        {
            if (str.Length > WIDTH_BYTE * 2)
                str = str.Trim();

            if (str.Length > WIDTH_BYTE * 2 && str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                str = str.Substring(2);

            byte[] bytes = Encoder.DecodeData(str);
            if (bytes.Length != WIDTH_BYTE)
                throw new FormatException("Invalid hex length");

            //Read bytes as littleEndian
            this.pn[4] = Utils.ToUInt32(bytes, 4 * 0, false);
            this.pn[3] = Utils.ToUInt32(bytes, 4 * 1, false);
            this.pn[2] = Utils.ToUInt32(bytes, 4 * 2, false);
            this.pn[1] = Utils.ToUInt32(bytes, 4 * 3, false);
            this.pn[0] = Utils.ToUInt32(bytes, 4 * 4, false);
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
            equals &= this.pn[0] == item.pn[0];
            equals &= this.pn[1] == item.pn[1];
            equals &= this.pn[2] == item.pn[2];
            equals &= this.pn[3] == item.pn[3];
            equals &= this.pn[4] == item.pn[4];
            return equals;
        }

        public static bool operator ==(uint160 a, uint160 b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;

            bool equals = true;
            equals &= a.pn[0] == b.pn[0];
            equals &= a.pn[1] == b.pn[1];
            equals &= a.pn[2] == b.pn[2];
            equals &= a.pn[3] == b.pn[3];
            equals &= a.pn[4] == b.pn[4];
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
            if (a.pn[4] < b.pn[4])
                return -1;
            if (a.pn[4] > b.pn[4])
                return 1;
            if (a.pn[3] < b.pn[3])
                return -1;
            if (a.pn[3] > b.pn[3])
                return 1;
            if (a.pn[2] < b.pn[2])
                return -1;
            if (a.pn[2] > b.pn[2])
                return 1;
            if (a.pn[1] < b.pn[1])
                return -1;
            if (a.pn[1] > b.pn[1])
                return 1;
            if (a.pn[0] < b.pn[0])
                return -1;
            if (a.pn[0] > b.pn[0])
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

            if (lendian)
            {
                Buffer.BlockCopy(Utils.ToBytes(this.pn[0], true), 0, arr, 4 * 0, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[1], true), 0, arr, 4 * 1, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[2], true), 0, arr, 4 * 2, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[3], true), 0, arr, 4 * 3, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[4], true), 0, arr, 4 * 4, 4);
            }
            else
            {
                Buffer.BlockCopy(Utils.ToBytes(this.pn[4], false), 0, arr, 4 * 0, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[3], false), 0, arr, 4 * 1, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[2], false), 0, arr, 4 * 2, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[1], false), 0, arr, 4 * 3, 4);
                Buffer.BlockCopy(Utils.ToBytes(this.pn[0], false), 0, arr, 4 * 4, 4);
            }

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
            return this.pn[0] | (ulong) this.pn[1] << 32;
        }

        public uint GetLow32()
        {
            return this.pn[0];
        }

        public override int GetHashCode()
        {
            //Since we are storing data in the array in big-endian format the first few bytes might be zero for PoW.
            //In order to reduce hash collision it is prefered to return last byte as a hash
            return (int)this.pn[4];
        }
    }
}