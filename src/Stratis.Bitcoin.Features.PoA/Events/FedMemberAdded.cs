using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.PoA.Events
{
    /// <summary>
    /// Event that is executed when a new federation member is added.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public class FedMemberAdded : EventBase
    {
        public IFederationMember AddedMember { get; }

        public FedMemberAdded(IFederationMember addedMember)
        {
            this.AddedMember = addedMember;
        }
    }
}
