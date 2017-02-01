using System;
using NBitcoin;

namespace Stratis.Bitcoin
{
	public class DateTimeProvider
	{
		public virtual long GetTime()
		{
			return DateTime.UtcNow.ToUnixTimestamp();
		}

		public static DateTimeProvider Default => new DateTimeProvider();
	}
}