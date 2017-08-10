using System;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreException : Exception
    {
        public IndexStoreException(string message) : base(message)
        {
        }
    }
}
