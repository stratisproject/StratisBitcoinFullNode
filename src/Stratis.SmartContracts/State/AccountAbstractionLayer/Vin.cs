using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class Vin
    {
        public uint256 Hash { get; set; }
        public uint Nvout { get; set; }
        public ulong Value { get; set; }
        public byte Alive { get; set; }
    }
}
