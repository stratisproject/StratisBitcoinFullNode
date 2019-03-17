using System;
using System.Globalization;
using System.Linq;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    /// <summary>
    /// Represent the challenge that miners must solve for finding a new block
    /// </summary>
    public class Target
    {
        private static Target _Difficulty1 = new Target(new byte[] { 0x1d, 0x00, 0xff, 0xff });
        public static Target Difficulty1
        {
            get
            {
                return _Difficulty1;
            }
        }

        public Target(uint compact)
            : this(ToBytes(compact))
        {

        }

        private static byte[] ToBytes(uint bits)
        {
            return new byte[]
            {
                (byte)(bits >> 24),
                (byte)(bits >> 16),
                (byte)(bits >> 8),
                (byte)(bits)
            };
        }


        private BigInteger _Target;

        public Target(byte[] compact)
        {
            if(compact.Length == 4)
            {
                byte exp = compact[0];
                var val = new BigInteger(compact.SafeSubarray(1, 3));
                this._Target = val.ShiftLeft(8  * (exp - 3));
            }
            else
                throw new FormatException("Invalid number of bytes");
        }        
        
        public Target(BigInteger target)
        {
            this._Target = target;
            this._Target = new Target(ToCompact())._Target;
        }
        public Target(uint256 target)
        {
            this._Target = new BigInteger(target.ToBytes(false));
            this._Target = new Target(ToCompact())._Target;
        }

        public static implicit operator Target(uint a)
        {
            return new Target(a);
        }
        public static implicit operator uint(Target a)
        {
            byte[] bytes = a._Target.ToByteArray();
            byte[] val = bytes.SafeSubarray(0, Math.Min(bytes.Length, 3));
            Array.Reverse(val);
            byte exp = (byte)(bytes.Length);
            if (exp == 1 && bytes[0] == 0)
                exp = 0;
            int missing = 4 - val.Length;
            if(missing > 0)
                val = val.Concat(new byte[missing]).ToArray();
            if(missing < 0)
                val = val.Take(-missing).ToArray();
            return (uint)val[0] + (uint)(val[1] << 8) + (uint)(val[2] << 16) + (uint)(exp << 24);
        }

        private double? _Difficulty;
        
        public double Difficulty
        {
            get
            {
                if(this._Difficulty == null)
                {
                    BigInteger[] qr = Difficulty1._Target.DivideAndRemainder(this._Target);
                    BigInteger quotient = qr[0];
                    BigInteger remainder = qr[1];
                    BigInteger decimalPart = BigInteger.Zero;
                    for(int i = 0; i < 12; i++)
                    {
                        BigInteger div = (remainder.Multiply(BigInteger.Ten)).Divide(this._Target);

                        decimalPart = decimalPart.Multiply(BigInteger.Ten);
                        decimalPart = decimalPart.Add(div);

                        remainder = remainder.Multiply(BigInteger.Ten).Subtract(div.Multiply(this._Target));
                    }

                    this._Difficulty = double.Parse(quotient.ToString() + "." + decimalPart.ToString(), new NumberFormatInfo()
                    {
                        NegativeSign = "-",
                        NumberDecimalSeparator = "."
                    });
                }
                return this._Difficulty.Value;
            }
        }

        public override bool Equals(object obj)
        {
            var item = obj as Target;
            if(item == null)
                return false;
            return this._Target.Equals(item._Target);
        }
        public static bool operator ==(Target a, Target b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a._Target.Equals(b._Target);
        }

        public static bool operator !=(Target a, Target b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this._Target.GetHashCode();
        }
        
        public BigInteger ToBigInteger()
        {
            return this._Target;
        }

        public uint ToCompact()
        {
            return (uint)this;
        }

        public uint256 ToUInt256()
        {
            return ToUInt256(this._Target);
        }

        internal static uint256 ToUInt256(BigInteger input)
        {
            byte[] array = input.ToByteArray();

            int missingZero = 32 - array.Length;

            if(missingZero < 0)
                throw new InvalidOperationException("Awful bug, this should never happen");

            if(missingZero != 0)
                return new uint256(new byte[missingZero].Concat(array), false);

            return new uint256(array, false);
        }

        public override string ToString()
        {
            return ToUInt256().ToString();
        }
    }
}
