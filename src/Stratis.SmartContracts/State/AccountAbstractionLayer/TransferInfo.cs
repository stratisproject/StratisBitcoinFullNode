using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class TransferInfo
    {
        public uint160 From { get; set; }
        public uint160 To { get; set; }
        public ulong Value { get; set; }
    }
}
