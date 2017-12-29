using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCBlock : IBitcoinSerializable
    {
        public BlockHeader BlockHeader { get; set; }

        public List<SCTransaction> Transactions { get; set; }

        public uint256 BlockHash { get; set; }

        public void ReadWrite(BitcoinStream stream)
        {
            throw new NotImplementedException();
        }
    }
}
