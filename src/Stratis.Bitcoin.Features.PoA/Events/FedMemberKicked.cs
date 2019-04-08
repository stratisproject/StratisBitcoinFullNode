using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.PoA.Events
{
    /// <summary>
    /// Event that is executed when federation member is kicked.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class FedMemberKicked : EventBase
    {
        public PubKey KickedMember { get; }

        public FedMemberKicked(PubKey kickedMember)
        {
            this.KickedMember = kickedMember;
        }
    }
}
