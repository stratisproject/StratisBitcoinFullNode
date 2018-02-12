using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Specifications
{
    /// <summary>
    /// An example interface for a smart contract standard. Devs will be able to import 'Stratis.SmartContracts.Specifications' 
    /// and use the interface on their smart contracts. Equivalent to ERC20 :)
    /// </summary>
    public interface IToken
    {
        void Transfer(Address to, ulong value);
    }
}
