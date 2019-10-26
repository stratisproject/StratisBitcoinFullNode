using System;

namespace Stratis.Bitcoin.Features.RPC.Exceptions
{
    public class NoEstimationException : Exception
    {
        public NoEstimationException(int nblock)
            : base("The FeeRate couldn't be estimated because of insufficient data from Bitcoin Core. Try to use smaller nBlock, or wait Bitcoin Core to gather more data.")
        {
        }
    }
}