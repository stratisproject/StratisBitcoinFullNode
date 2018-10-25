using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools
{
    public class EditableTimeProvider : DateTimeProvider
    {
        public TimeSpan AdjustedTimeOffset
        {
            get { return this.adjustedTimeOffset; }
            set { this.adjustedTimeOffset = value; }
        }
    }
}
