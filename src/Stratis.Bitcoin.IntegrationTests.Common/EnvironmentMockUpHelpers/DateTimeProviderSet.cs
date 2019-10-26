using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class DateTimeProviderSet : DateTimeProvider
    {
        public long time;
        public DateTime timeutc;

        public override long GetTime()
        {
            return this.time;
        }

        public override DateTime GetUtcNow()
        {
            return this.timeutc;
        }
    }
}
