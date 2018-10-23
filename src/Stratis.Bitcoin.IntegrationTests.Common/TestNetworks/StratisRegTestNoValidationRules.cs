using System;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.TestNetworks
{
    public sealed class StratisRegTestNoValidationRules : StratisRegTest
    {
        public StratisRegTestNoValidationRules()
        {
            this.Name = Guid.NewGuid().ToString();
        }
    }
}
