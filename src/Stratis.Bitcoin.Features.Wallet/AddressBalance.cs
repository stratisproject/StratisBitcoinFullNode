﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A class that represents the balance of an address.
    /// </summary>
    public class AddressBalance
    {
        /// <summary>
        /// The address for which the balance is calculated.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public Money AmountConfirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public Money AmountUnconfirmed { get; set; }
    }
}