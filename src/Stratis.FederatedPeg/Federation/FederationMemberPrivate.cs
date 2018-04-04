using System.Security;
using NBitcoin;

namespace Stratis.FederatedPeg
{
    /// <summary>
    /// The private version of the FederationMember. Inlcludes the private key and the passPhrase used to 
    /// encrypt it on disk.
    /// </summary>
    public class FederationMemberPrivate
    {
        /// <summary>
        /// Name of the federation member.  This enables the Sidechain Generator actor to track which members
        /// he has received keys from.
        /// </summary>
        public string Name { get; }
        
        // The pass word/phrase used.
        private string PassPhrase { get; }

        // Mainchain private key.
        private Key PrivateKeyMainchain { set; get; }

        // Sidechain private key.
        private Key PrivateKeySidechain { set; get; }

        //The public version of the FederationMemeber.  Does not have private key.
        private readonly FederationMember federationMember;

        // Private constructor called from the CreateNew method. 
        internal FederationMemberPrivate(string name, string passPhrase, Key privateKeyMainchain, Key privateKeySidechain)
        {
            this.Name = name;
            this.PassPhrase = passPhrase;

            this.PrivateKeyMainchain = privateKeyMainchain;
            this.PrivateKeySidechain = privateKeySidechain;

            this.federationMember = new FederationMember(this.Name, privateKeyMainchain.PubKey, privateKeySidechain.PubKey);
        }

        //Uses the encryption provider to encypt the private key with the passPhrase.
        internal string GetEncryptedKey(Chain chain)
        {
            string key = chain == Chain.Mainchain ? this.PrivateKeyMainchain.ToHex() : this.PrivateKeySidechain.ToHex();
            return EncryptionProvider.EncryptString(key, this.PassPhrase);
        }

        /// <summary>
        /// Creates a new FederationMember and generates new private keys.
        /// </summary>
        /// <param name="name">Name of the federation member.</param>
        /// <param name="passPhrase">Password to encrypt the file on disk.</param>
        /// <returns>The newly created private FederationMember.</returns>
        public static FederationMemberPrivate CreateNew(string name, string passPhrase)
        {
            return new FederationMemberPrivate(name, passPhrase, new Key(), new Key());
        }

        /// <summary>
        /// Gets the public version of the FederationMember without any private key info.
        /// </summary>
        /// <returns>Public version of the FederationMember.</returns>
        public FederationMember ToFederationMember()
        {
            return this.federationMember;
        }

        /// <summary>
        /// Indicates whether a name is valid.
        /// </summary>
        /// <param name="name">Federation member name.</param>
        /// <returns>False if the name is not valid.</returns>
        // ToDo: need to also exclude illegal file system characters.
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Length < 3 || string.IsNullOrWhiteSpace(name)) return false;
            if (name.Contains("_")) return false;
            return true;
        }
    }
}