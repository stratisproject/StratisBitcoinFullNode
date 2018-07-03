using System;
using System.Collections.Generic;

namespace NBitcoin.OpenAsset
{
    public class AssetMoney : IComparable, IComparable<AssetMoney>, IEquatable<AssetMoney>, IMoney
    {
        private long _Quantity;
        public long Quantity
        {
            get
            {
                return this._Quantity;
            }
            // used as a central point where long.MinValue checking can be enforced 
            private set
            {
                CheckLongMinValue(value);
                this._Quantity = value;
            }
        }
        private static void CheckLongMinValue(long value)
        {
            if(value == long.MinValue)
                throw new OverflowException("satoshis amount should be greater than long.MinValue");
        }

        private readonly AssetId _Id;

        /// <summary>
        /// AssetId of the current amount
        /// </summary>
        public AssetId Id
        {
            get
            {
                return this._Id;
            }
        }

        /// <summary>
        /// Get absolute value of the instance
        /// </summary>
        /// <returns></returns>
        public AssetMoney Abs()
        {
            AssetMoney a = this;
            if(a.Quantity < 0)
                a = -a;
            return a;
        }
        #region ctor

        public AssetMoney(AssetId assetId)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;
        }

        public AssetMoney(IDestination issuer, long quantity)
            : this(new AssetId(issuer), quantity)
        {
        }
        public AssetMoney(AssetId assetId, int quantity)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;
            this.Quantity = quantity;
        }

        public AssetMoney(AssetId assetId, uint quantity)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;
            this.Quantity = quantity;
        }
        public AssetMoney(AssetId assetId, long quantity)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;
            this.Quantity = quantity;
        }

        public AssetMoney(AssetId assetId, ulong quantity)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;

            // overflow check. 
            // ulong.MaxValue is greater than long.MaxValue
            checked
            {
                this.Quantity = (long)quantity;
            }
        }

        public AssetMoney(AssetId assetId, decimal amount, int divisibility)
        {
            if(assetId == null)
                throw new ArgumentNullException("assetId");
            this._Id = assetId;
            // sanity check. Only valid units are allowed
            checked
            {
                int dec = Pow10(divisibility);
                decimal satoshi = amount * dec;
                this.Quantity = (long)satoshi;
            }
        }
        
        #endregion

        private static int Pow10(int divisibility)
        {
            if(divisibility < 0)
                throw new ArgumentOutOfRangeException("divisibility", "divisibility should be higher than 0");
            int dec = 1;
            for(int i = 0; i < divisibility; i++)
            {
                dec = dec * 10;
            }
            return dec;
        }


        /// <summary>
        /// Split the Money in parts without loss
        /// </summary>
        /// <param name="parts">The number of parts (must be more than 0)</param>
        /// <returns>The splitted money</returns>
        public IEnumerable<AssetMoney> Split(int parts)
        {
            if(parts <= 0)
                throw new ArgumentOutOfRangeException("Parts should be more than 0", "parts");
            long remain;
            long result = DivRem(this._Quantity, parts, out remain);

            for(int i = 0; i < parts; i++)
            {
                yield return new AssetMoney(this._Id, result + (remain > 0 ? 1 : 0));
                remain--;
            }
        }

        private static long DivRem(long a, long b, out long result)
        {
            result = a % b;
            return a / b;
        }

        public decimal ToDecimal(int divisibility)
        {
            int dec = Pow10(divisibility);
            // overflow safe because (long / int) always fit in decimal 
            // decimal operations are checked by default
            return (decimal) this.Quantity / (int)dec;
        }

        #region IEquatable<AssetMoney> Members

        public bool Equals(AssetMoney other)
        {
            if(other == null)
                return false;
            CheckAssetId(other, "other");
            return this._Quantity.Equals(other.Quantity);
        }

        internal void CheckAssetId(AssetMoney other, string param)
        {
            if(other.Id != this.Id)
                throw new ArgumentException("AssetMoney instance of different assets can't be computed together", param);
        }

        public int CompareTo(AssetMoney other)
        {
            if(other == null)
                return 1;
            CheckAssetId(other, "other");
            return this._Quantity.CompareTo(other.Quantity);
        }

        #endregion

        #region IComparable Members

        public int CompareTo(object obj)
        {
            if(obj == null)
                return 1;
            var m = obj as AssetMoney;
            if(m != null)
                return this._Quantity.CompareTo(m.Quantity);
#if !NETCORE
            return _Quantity.CompareTo(obj);
#else
            return this._Quantity.CompareTo((long)obj);
#endif
        }

        #endregion

        public static AssetMoney operator -(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return new AssetMoney(left.Id, checked(left.Quantity - right.Quantity));
        }

        public static AssetMoney operator -(AssetMoney left)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            return new AssetMoney(left.Id, checked(-left.Quantity));
        }

        public static AssetMoney operator +(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return new AssetMoney(left.Id, checked(left.Quantity + right.Quantity));
        }

        public static AssetMoney operator *(int left, AssetMoney right)
        {
            if(right == null)
                throw new ArgumentNullException("right");
            return new AssetMoney(right.Id, checked(left * right.Quantity));
        }

        public static AssetMoney operator *(AssetMoney right, int left)
        {
            if(right == null)
                throw new ArgumentNullException("right");
            return new AssetMoney(right.Id, checked(right.Quantity * left));
        }

        public static AssetMoney operator *(long left, AssetMoney right)
        {
            if(right == null)
                throw new ArgumentNullException("right");
            return new AssetMoney(right.Id, checked(left * right.Quantity));
        }

        public static AssetMoney operator *(AssetMoney right, long left)
        {
            if(right == null)
                throw new ArgumentNullException("right");
            return new AssetMoney(right.Id, checked(left * right.Quantity));
        }

        public static bool operator <(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return left.Quantity < right.Quantity;
        }

        public static bool operator >(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return left.Quantity > right.Quantity;
        }

        public static bool operator <=(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return left.Quantity <= right.Quantity;
        }

        public static bool operator >=(AssetMoney left, AssetMoney right)
        {
            if(left == null)
                throw new ArgumentNullException("left");
            if(right == null)
                throw new ArgumentNullException("right");
            left.CheckAssetId(right, "right");
            return left.Quantity >= right.Quantity;
        }

        public override bool Equals(object obj)
        {
            var item = obj as AssetMoney;
            if(item == null)
                return false;
            if(item.Id != this.Id)
                return false;
            return this._Quantity.Equals(item.Quantity);
        }

        public static bool operator ==(AssetMoney a, AssetMoney b)
        {
            if(ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;

            if(a.Id != b.Id)
                return false;
            return a.Quantity == b.Quantity;
        }

        public static bool operator !=(AssetMoney a, AssetMoney b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Tuple.Create(this._Quantity, this.Id).GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("{0}-{1}", this.Quantity, this.Id);
        }

        public static AssetMoney Min(AssetMoney a, AssetMoney b)
        {
            if(a == null)
                throw new ArgumentNullException("a");
            if(b == null)
                throw new ArgumentNullException("b");
            a.CheckAssetId(b, "b");
            if(a <= b)
                return a;
            return b;
        }

        #region IMoney Members


        IMoney IMoney.Add(IMoney money)
        {
            var assetMoney = (AssetMoney)money;
            return this + assetMoney;
        }

        IMoney IMoney.Sub(IMoney money)
        {
            var assetMoney = (AssetMoney)money;
            return this - assetMoney;
        }

        IMoney IMoney.Negate()
        {
            return this * -1;
        }

        int IComparable.CompareTo(object obj)
        {
            return CompareTo(obj);
        }

        int IComparable<IMoney>.CompareTo(IMoney other)
        {
            return CompareTo(other);
        }

        bool IEquatable<IMoney>.Equals(IMoney other)
        {
            return Equals(other);
        }

        bool IMoney.IsCompatible(IMoney money)
        {
            if(money == null)
                throw new ArgumentNullException("money");
            var assetMoney = money as AssetMoney;
            if(assetMoney == null)
                return false;
            return assetMoney.Id == this.Id;
        }

        #endregion

        #region IMoney Members


        IEnumerable<IMoney> IMoney.Split(int parts)
        {
            return Split(parts);
        }

        #endregion
    }
}
