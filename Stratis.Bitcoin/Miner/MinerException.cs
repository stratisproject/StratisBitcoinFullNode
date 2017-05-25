using System;

namespace Stratis.Bitcoin.Miner
{
    public class MinerException : Exception
    {
		public MinerException(string message) : base(message)
		{ }
	}
}
