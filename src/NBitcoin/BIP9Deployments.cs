using System;

namespace NBitcoin
{
    public class BIP9DeploymentsParameters
    {
        /// <summary>Special flag for timeout to indicate always active.</summary>
        public const long AlwaysActive = -1;

        // 95% of 2016 blocks
        public const long DefaultMainnetThreshold = 1916;

        // 75% of 2016 blocks
        public const long DefaultTestnetThreshold = 1512;

        // 75% of 144 blocks
        public const long DefaultRegTestThreshold = 108;
        
        public BIP9DeploymentsParameters(string name, int bit, DateTimeOffset startTime, DateTimeOffset timeout, long threshold)
        {
            this.Bit = bit;
            this.StartTime = startTime;
            this.Timeout = timeout;
            this.Threshold = threshold;
            this.Name = name.ToLower();
        }
        
        public BIP9DeploymentsParameters(string name, int bit, long startTime, long timeout, long threshold)
            : this(name, bit, (DateTimeOffset) Utils.UnixTimeToDateTime(startTime), Utils.UnixTimeToDateTime(timeout), threshold)
        {
        }

        /// <summary>Determines which bit in the nVersion field of the block is to be used to signal the soft fork lock-in and activation. It is chosen from the set {0,1,2,...,28}.</summary>
        public int Bit
        {
            get;
            private set;
        }

        /// <summary>Specifies a minimum median time past of a block at which the bit gains its meaning.</summary>
        public DateTimeOffset StartTime
        {
            get;
            private set;
        }

        /// <summary>Specifies a time at which the deployment is considered failed. If the median time past of a block >= timeout and the soft fork has not yet locked in
        /// (including that block's bit state), the deployment is considered failed on all descendants of the block.</summary>
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


        /// <summary>Specifies the activation threshold for this deployment. The BIP9 specification originally set the threshold at >=1916 blocks (95% of 2016),
        /// or >=1512 for testnet (75% of 2016). </summary>
        public long Threshold
        {
            get;
            private set;
        }
    }
}
