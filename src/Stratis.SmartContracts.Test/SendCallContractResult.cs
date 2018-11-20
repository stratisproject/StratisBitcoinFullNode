using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Test
{
    public class SendCallContractResult
    {
        public ulong Fee { get; set; }
        public string Hex { get; set; }
        public string Message { get; set; }
        public bool Success { get; set; }
        public uint256 TransactionId { get; set; }
    }
}
