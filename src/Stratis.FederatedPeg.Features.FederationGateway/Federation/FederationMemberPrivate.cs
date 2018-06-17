using System.Security;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.Federation
{
    /// <summary>
    /// The private version of the FederationMember. Includes the private key and the password used to 
    /// encrypt it on disk.
    /// </summary>
    public class FederationMemberPrivate
    {
        /// <summary>
        /// Name of the federation member.  This enables the Sidechain Generator actor to track which members
        /// he has received keys from.
        /// </summary>
        public string Name { get; }
        
        // The password used.
        private string Password { get; }

        // Mainchain private key.
        private Key PrivateKeyMainchain { set; get; }

        // Sidechain private key.
        private Key PrivateKeySidechain { set; get; }

        //The public version of the FederationMemeber.  Does not have private key.
        private readonly FederationMember federationMember;

        // Constructor called from the CreateNew method. 
        public FederationMemberPrivate(string name, string password, Key privateKeyMainchain, Key privateKeySidechain)
        {
            this.Name = name;
            this.Password = password;

            this.PrivateKeyMainchain = privateKeyMainchain;
            this.PrivateKeySidechain = privateKeySidechain;

            this.federationMember = new FederationMember(this.Name, privateKeyMainchain.PubKey, privateKeySidechain.PubKey);
        }
        
        /// <summary>
        /// Creates a new FederationMember and generates new private keys.
        /// </summary>
        /// <param name="name">Name of the federation member.</param>
        /// <param name="password">Password to encrypt the file on disk.</param>
        /// <returns>The newly created private FederationMember.</returns>
        public static FederationMemberPrivate CreateNew(string name, string password)
        {
            return new FederationMemberPrivate(name, password, new Key(), new Key());
        }

        /// <summary>
        /// Gets the public version of the FederationMember without any private key info.
        /// </summary>
        /// <returns>Public version of the FederationMember.</returns>
        public FederationMember ToFederationMember()
        {
            return this.federationMember;
        }
    }
}