using System;

namespace NBitcoin
{
    public class FeeRate : IEquatable<FeeRate>, IComparable<FeeRate>
    {
        private readonly Money _FeePerK;
        /// <summary>
        /// Fee per KB
        /// </summary>
        public Money FeePerK
        {
            get
            {
                return this._FeePerK;
            }
        }

        private readonly static FeeRate _Zero = new FeeRate(Money.Zero);
        public static FeeRate Zero
        {
            get
            {
                return _Zero;
            }
        }

        public FeeRate(Money feePerK)
        {
            if(feePerK == null)
                throw new ArgumentNullException("feePerK");
            if(feePerK.Satoshi < 0)
                throw new ArgumentOutOfRangeException("feePerK");
            this._FeePerK = feePerK;
        }

        public FeeRate(Money feePaid, int size)
        {
            if(feePaid == null)
                throw new ArgumentNullException("feePaid");
            if(feePaid.Satoshi < 0)
                throw new ArgumentOutOfRangeException("feePaid");
            if(size > 0)
                this._FeePerK = (long)(feePaid.Satoshi / (decimal)size * 1000);
            else
                this._FeePerK = 0;
        }

        /// <summary>
        /// Get fee for the size
        /// </summary>
        /// <param name="virtualSize">Size in bytes</param>
        /// <returns></returns>
        public Money GetFee(int virtualSize)
        {
            Money nFee = this._FeePerK.Satoshi * virtualSize / 1000;
            if(nFee == 0 && this._FeePerK.Satoshi > 0)
                nFee = this._FeePerK.Satoshi;
            return nFee;
        }
        public Money GetFee(Transaction tx)
        {
            return GetFee(tx.GetVirtualSize());
        }

        public override bool Equals(object obj)
        {
            if(ReferenceEquals(this, obj))
                return true;
            if(((object)this == null) || (obj == null))
                return false;
            FeeRate left = this;
            var right = obj as FeeRate;
            if(right == null)
                return false;
            return left._FeePerK == right._FeePerK;
        }

        public override string ToString()
        {
            return String.Format("{0} BTC/kB", this._FeePerK.ToString());
        }

        #region IEquatable<FeeRate> Members

        public bool Equals(FeeRate other)
        {
            return other != null && this._FeePerK.Equals(other._FeePerK);
        }

        public int CompareTo(FeeRate other)
        {
            return other == null
                ? 1
                : this._FeePerK.CompareTo(other._FeePerK);
        }

        #endregion

        #region IComparable Members

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            var m = obj as FeeRate;
            if (m != null)
                return this._FeePerK.CompareTo(m._FeePerK);
#if !NETCORE
            return _FeePerK.CompareTo(obj);
#else
            return this._FeePerK.CompareTo((long)obj);
#endif
        }

        #endregion

        public static bool operator <(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left._FeePerK < right._FeePerK;
        }
        public static bool operator >(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left._FeePerK > right._FeePerK;
        }
        public static bool operator <=(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left._FeePerK <= right._FeePerK;
        }
        public static bool operator >=(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left._FeePerK >= right._FeePerK;
        }

        public static bool operator ==(FeeRate left, FeeRate right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (((object)left == null) || ((object)right == null))
                return false;
            return left._FeePerK == right._FeePerK;
        }

        public static bool operator !=(FeeRate left, FeeRate right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            return this._FeePerK.GetHashCode();
        }

        public static FeeRate Min(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left <= right
                ? left
                : right;
        }

        public static FeeRate Max(FeeRate left, FeeRate right)
        {
            if (left == null)
                throw new ArgumentNullException("left");
            if (right == null)
                throw new ArgumentNullException("right");
            return left >= right
                ? left
                : right;
        }
    }
}
