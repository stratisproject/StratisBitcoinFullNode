using System;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreException : Exception
    {
        public ProvenBlockHeaderStoreException(string message) : base(message)
        {
        }
    }
}
