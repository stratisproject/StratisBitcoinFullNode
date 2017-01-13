using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    public static class Check
    {
		public static void Assert(bool v)
		{
			if (!v)
				throw new Exception("Assertion failed");
		}
	}
}
