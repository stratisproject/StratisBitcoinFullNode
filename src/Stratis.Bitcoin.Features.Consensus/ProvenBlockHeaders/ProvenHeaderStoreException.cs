using System;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenHeaderStoreException : Exception
    {
        public ProvenHeaderStoreException(string message) : base(message)
        {
        }
    }
}
