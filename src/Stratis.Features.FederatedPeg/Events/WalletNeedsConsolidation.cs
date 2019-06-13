using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Features.FederatedPeg.Events
{
    public class WalletNeedsConsolidation : EventBase
    {
        public Money Amount { get; }

        public WalletNeedsConsolidation(Money amount)
        {
            this.Amount = amount;
        }

    }
}
