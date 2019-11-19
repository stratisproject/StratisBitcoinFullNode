using System;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class BlockNotFoundException : Exception
    {
        public BlockNotFoundException(string message) : base(message)
        {
        }
    }
}
