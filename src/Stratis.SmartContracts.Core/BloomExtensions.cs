using System.Collections.Generic;
using DBreeze.Utils;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public static class BloomExtensions
    {
        public static bool Test(this Bloom bloom, uint160 address) => Test(bloom, new [] { address }, null);

        public static bool Test(this Bloom bloom, IEnumerable<uint160> addresses) => Test(bloom, addresses, null);

        public static bool Test(this Bloom bloom, IEnumerable<byte[]> topics) => Test(bloom, (IEnumerable<uint160>)null, topics);

        public static bool Test(this Bloom bloom, uint160 address, IEnumerable<byte[]> topics) => Test(bloom, new[] { address }, topics);

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

            // TODO Use Bloom.Test(Bloom other) in future.
            filterBloom.Or(bloom);

            return bloom == filterBloom;
        }
    }
}
