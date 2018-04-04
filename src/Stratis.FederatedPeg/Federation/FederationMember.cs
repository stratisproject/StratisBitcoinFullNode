using NBitcoin;

namespace Stratis.FederatedPeg
{
    // Todo: Consider renaming to FederationMemberPublic?
    /// <summary>
    /// The FederationMember class represents the public details held by a member of the federation.
    /// </summary>
    public class FederationMember
    {
        /// <summary>
        /// Name used to distinguish the member from other members in the federation.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Public key used for the Mainchain multi-sig address.
        /// </summary>
        public PubKey PublicKeyMainChain { get; }

        /// <summary>
        /// Public key used for the Sidechain multi-sig address.
        /// </summary>
        public PubKey PublicKeySideChain { get; }
        
        /// <summary>
        /// Create a federation member with only the publicly known keys.
        /// </summary>
        /// <param name="name">The name used to identify the member.</param>
        /// <param name="publicKeyMainchain">The public key used for the multi-sig address on the Mainchain.</param>
        /// <param name="publicKeySidechain">The public key used for the multi-sig address on the Sidechain.</param>
        internal FederationMember(string name, PubKey publicKeyMainchain, PubKey publicKeySidechain)
        {
            this.Name = name;
            this.PublicKeyMainChain = publicKeyMainchain;
            this.PublicKeySideChain = publicKeySidechain;
        }
    }
}
