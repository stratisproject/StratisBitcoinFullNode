using System;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.TestNetworks
{
    public sealed class BitcoinRegTestNoValidationRules : BitcoinRegTest
    {
        public BitcoinRegTestNoValidationRules()
        {
            this.Name = Guid.NewGuid().ToString();
        }
    }
}
