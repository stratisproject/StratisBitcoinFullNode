using System;

namespace NBitcoin
{
    public struct LockTime : IBitcoinSerializable
    {
        internal const uint LOCKTIME_THRESHOLD = 500000000; // Tue Nov  5 00:53:20 1985 UTC
        private uint _value;

        public static LockTime Zero
        {
            get
            {
                return new LockTime((uint)0);
            }
        }
        public LockTime(DateTimeOffset dateTime)
        {
            this._value = Utils.DateTimeToUnixTime(dateTime);
            if(this._value < LOCKTIME_THRESHOLD)
                throw new ArgumentOutOfRangeException("dateTime", "The minimum possible date is be Tue Nov  5 00:53:20 1985 UTC");
        }
        public LockTime(int valueOrHeight)
        {
            this._value = (uint)valueOrHeight;
        }
        public LockTime(uint valueOrHeight)
        {
            this._value = valueOrHeight;
        }


        public DateTimeOffset Date
        {
            get
            {
                if(!this.IsTimeLock)
                    throw new InvalidOperationException("This is not a time based lock");
                return Utils.UnixTimeToDateTime(this._value);
            }
        }

        public int Height
        {
            get
            {
                if(!this.IsHeightLock)
                    throw new InvalidOperationException("This is not a height based lock");
                return (int) this._value;
            }
        }

        public uint Value
        {
            get
            {
                return this._value;
            }
        }


        public bool IsHeightLock
        {
            get
            {
                return this._value < LOCKTIME_THRESHOLD; // Tue Nov  5 00:53:20 1985 UTC
            }
        }

        public bool IsTimeLock
        {
            get
            {
                return !this.IsHeightLock;
            }
        }


        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._value);
        }

        #endregion

        public override string ToString()
        {
            return this.IsHeightLock ? "Height : " + this.Height : "Date : " + this.Date;
        }

        public static implicit operator LockTime(int valueOrHeight)
        {
            return new LockTime(valueOrHeight);
        }

        public static implicit operator LockTime(DateTimeOffset date)
        {
            return new LockTime(date);
        }

        public static implicit operator LockTime(uint valueOrHeight)
        {
            return new LockTime(valueOrHeight);
        }

        public static implicit operator DateTimeOffset(LockTime lockTime)
        {
            return lockTime.Date;
        }
        public static implicit operator int(LockTime lockTime)
        {
            return (int)lockTime._value;
        }

        public static implicit operator uint(LockTime lockTime)
        {
            return lockTime._value;
        }

        public static implicit operator long(LockTime lockTime)
        {
            return (long)lockTime._value;
        }

        public override bool Equals(object obj)
        {
            if(!(obj is LockTime))
                return false;
            var item = (LockTime)obj;
            return this._value.Equals(item._value);
        }
        public static bool operator ==(LockTime a, LockTime b)
        {
            return a._value == b._value;
        }

        public static bool operator !=(LockTime a, LockTime b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this._value.GetHashCode();
        }
    }
}
