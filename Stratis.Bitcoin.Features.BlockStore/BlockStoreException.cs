namespace Stratis.Bitcoin.Features.BlockStore
{
    using System;

    public class BlockStoreException : Exception
    {
        public BlockStoreException(string message) : base(message)
        {
        }
    }
}
