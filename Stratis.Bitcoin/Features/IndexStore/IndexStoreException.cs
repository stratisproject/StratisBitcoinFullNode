using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreException : Exception
    {
        public IndexStoreException(string message) : base(message)
        { }
    }
}
