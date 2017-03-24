using System;
using NBitcoin;

namespace Stratis.Bitcoin
{
    public interface IDateTimeProvider
    {
        long GetTime();
        DateTimeOffset GetTimeOffset();
        DateTime GetUtcNow();
    }

    public class DateTimeProvider : IDateTimeProvider
	{
		public virtual long GetTime()
		{
			return DateTime.UtcNow.ToUnixTimestamp();
		}

		public virtual DateTime GetUtcNow()
		{
			return DateTime.UtcNow;
		}

		public virtual DateTimeOffset GetTimeOffset()
		{
			return DateTimeOffset.UtcNow;
		}

		public static IDateTimeProvider Default => new DateTimeProvider();
	}
}