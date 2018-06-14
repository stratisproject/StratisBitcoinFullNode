using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Math.Field
{
    internal class GF2Polynomial
        : IPolynomial
    {
        protected readonly int[] exponents;

        internal GF2Polynomial(int[] exponents)
        {
            this.exponents = Arrays.Clone(exponents);
        }

        public virtual int Degree
        {
            get
            {
                return this.exponents[this.exponents.Length - 1];
            }
        }

        public virtual int[] GetExponentsPresent()
        {
            return Arrays.Clone(this.exponents);
        }

        public override bool Equals(object obj)
        {
            if(this == obj)
            {
                return true;
            }
            var other = obj as GF2Polynomial;
            if(null == other)
            {
                return false;
            }
            return Arrays.AreEqual(this.exponents, other.exponents);
        }

        public override int GetHashCode()
        {
            return Arrays.GetHashCode(this.exponents);
        }
    }
}
