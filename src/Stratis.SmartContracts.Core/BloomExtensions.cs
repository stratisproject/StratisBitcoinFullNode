using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public static class BloomExtensions
    {
        public static bool Test(this Bloom bloom, uint160 address) => Test(bloom, address, null);

        public static bool Test(this Bloom bloom, IEnumerable<byte[]> topics) => Test(bloom, null, topics);

        public static bool Test(this Bloom bloom, uint160 address, IEnumerable<byte[]> topics)
        {
            var filterBloom = new Bloom();

            if (address != null)
            {
                filterBloom.Add(address.ToBytes());
            }

            if (topics != null)
            {
                foreach (byte[] topic in topics)
                {
                    filterBloom.Add(topic);
                }
            }

            // TODO Use Bloom.Test(Bloom other) in future.
            filterBloom.Or(bloom);

            return bloom == filterBloom;
        }
    }
}
