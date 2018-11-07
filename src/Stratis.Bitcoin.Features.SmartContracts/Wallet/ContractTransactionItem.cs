﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public class ContractTransactionItem
    {
        public uint BlockHeight { get; set; }
        public ContractTransactionItemType Type { get; set; }
        public uint256 Hash { get; set; }
        public string To { get; set; }
        public decimal Amount { get; set; }
    }
}
