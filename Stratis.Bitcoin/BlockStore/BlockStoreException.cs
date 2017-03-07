using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockStore
{
    public class BlockStoreException : Exception
    {
		public BlockStoreException(string message) : base(message)
		{ }
	}
}
