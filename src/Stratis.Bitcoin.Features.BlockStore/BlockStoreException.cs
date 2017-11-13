using System;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreException : Exception
    {
        public BlockStoreException(string message) : base(message)
        {
        }
    }
}
