using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Specifications
{
    public interface IToken
    {
        void Transfer(Address to, ulong value);
    }
}
