using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.PoA.Events
{
    /// <summary>
    /// Event that is executed when a new federation member is added.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class FedMemberAdded : EventBase
    {
        public PubKey AddedMember { get; }

        public FedMemberAdded(PubKey addedMember)
        {
            this.AddedMember = addedMember;
        }
    }
}
