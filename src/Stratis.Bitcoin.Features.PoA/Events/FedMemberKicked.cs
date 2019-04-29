using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.PoA.Events
{
    /// <summary>
    /// Event that is executed when federation member is kicked.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class FedMemberKicked : EventBase
    {
        public IFederationMember KickedMember { get; }

        public FedMemberKicked(IFederationMember  kickedMember)
        {
            this.KickedMember = kickedMember;
        }
    }
}
