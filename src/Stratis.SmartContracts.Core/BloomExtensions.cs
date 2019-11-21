using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public static class BloomExtensions
    {
        public static bool Test(this Bloom bloom, uint160 address) => Test(bloom, new [] { address }, null);

        public static bool Test(this Bloom bloom, IEnumerable<uint160> addresses) => Test(bloom, addresses, null);

        public static bool Test(this Bloom bloom, IEnumerable<byte[]> topics) => Test(bloom, (IEnumerable<uint160>)null, topics);

        public static bool Test(this Bloom bloom, uint160 address, IEnumerable<byte[]> topics) => Test(bloom, new[] { address }, topics);

        /// <summary>
        /// Tests if the address AND all of the topics are matched by the filter.
        /// </summary>
        /// <param name="bloom">The filter to match.</param>
        /// <param name="addresses">The addresses to match against the filter.</param>
        /// <param name="topics">The topics to match against the filter.</param>
        /// <returns>True if all of the filter parameters match.</returns>
        public static bool Test(this Bloom bloom, IEnumerable<uint160> addresses, IEnumerable<byte[]> topics)
        {
            var filterBloom = new Bloom();

            if (addresses != null)
            {
                foreach (uint160 address in addresses)
                {
                    if (address != null)
                    {
                        filterBloom.Add(address.ToBytes());
                    }
                }
            }

            if (topics != null)
            {
                foreach (byte[] topic in topics)
                {
                    if (topic != null)
                    {
                        filterBloom.Add(topic);
                    }
                }
            }

            return bloom.Test(filterBloom);
        }
    }
}
