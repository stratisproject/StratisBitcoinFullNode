using System;

namespace NBitcoin
{
    public class BIP9DeploymentsParameters
    {
        /// <summary>Special flag for timeout to indicate always active.</summary>
        public const long AlwaysActive = -1;

        public BIP9DeploymentsParameters(string name, int bit, DateTimeOffset startTime, DateTimeOffset timeout)
        {
            this.Bit = bit;
            this.StartTime = startTime;
            this.Timeout = timeout;
            this.Name = name.ToLower();
        }

        public BIP9DeploymentsParameters(string name, int bit, long startTime, long timeout)
            : this(name, bit, (DateTimeOffset) Utils.UnixTimeToDateTime(startTime), Utils.UnixTimeToDateTime(timeout))
        {

        }

        public int Bit
        {
            get;
            private set;
        }

        public DateTimeOffset StartTime
        {
            get;
            private set;
        }

        public DateTimeOffset Timeout
        {
            get;
            private set;
        }

        public string Name 
        {
            get;
            private set;
        }

    }
}
