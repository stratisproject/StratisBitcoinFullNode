using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;

//ToDo: Create a private MemberFolderManager to just read the private folder that an fed member has.

namespace Stratis.FederatedPeg
{
    /// <summary>
    /// The MemberFolderManager points to a folder to create and manage keys and addresses of the federation.
    /// </summary>
    public class MemberFolderManager
    {
        //filename templates.
        //{0} = Mainchain or Sidechain, {1} = federation member name.
        private const string Filename_Template_Public = "PUBLIC_{0}_{1}.txt";
        private const string Filename_Template_Private = "PRIVATE_DO_NOT_SHARE_{0}_{1}.txt";

        /// <summary>
        /// The folder to manage.
        /// </summary>
        private readonly string folder;

        /// <summary>
        /// Creates a member folder.
        /// </summary>
        /// <param name="folder">The folder to manage.</param>
        public MemberFolderManager(string folder)
        {
            if (folder == string.Empty)
                folder = Environment.CurrentDirectory;

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException();
            this.folder = folder;
        }

        /// <summary>
        /// Counts the number of keys for the specified member name.
        /// </summary>
        /// <param name="nameOfMember">Federation member name.</param>
        /// <returns>Number of key files found that include the member name.</returns>
        public int CountKeyFilesForMember(string nameOfMember)
        {
            string[] files = Directory.GetFiles(this.folder, $"*chain_{nameOfMember}.txt");
            return files.Length;
        }

        /// <summary>
        /// Outputs private and public keys for each chain.
        /// </summary>
        /// <param name="federationMemberPrivate">The federation member to output keys for.</param>
        public void OutputKeys(FederationMemberPrivate federationMemberPrivate)
        {
            // Public keys.
            OutputKey(Chain.Mainchain, 
                federationMemberPrivate.Name,
                Filename_Template_Public,
                federationMemberPrivate.ToFederationMember().PublicKeyMainChain.ToString());

            OutputKey(Chain.Sidechain,
                federationMemberPrivate.Name,
                Filename_Template_Public,
                federationMemberPrivate.ToFederationMember().PublicKeySideChain.ToString());

            //Private keys.
            OutputKey(Chain.Mainchain,
                federationMemberPrivate.Name,
                Filename_Template_Private,
                federationMemberPrivate.GetEncryptedKey(Chain.Mainchain));

            OutputKey(Chain.Sidechain,
                federationMemberPrivate.Name,
                Filename_Template_Private,
                federationMemberPrivate.GetEncryptedKey(Chain.Sidechain));
        }

        // Outputs key based on the chain and a template.
        private void OutputKey(Chain chain, string name, string template, string content)
        {
            File.WriteAllText(
                Path.Combine(this.folder, string.Format(template, chain.ToString(), name)), 
                content);
        }

        /// <summary>
        /// Creates the members from the specified folder.
        /// </summary>
        /// <returns></returns>
        public List<FederationMember> LoadMembers()
        {
            var list = new List<FederationMember>();
            string[] files = Directory.GetFiles(this.folder, $"PUBLIC_mainchain*.txt");
            foreach (string file in files)
                list.Add(LoadMember(file));
            return list;
        }

        // Loads member information from the specified file.
        private FederationMember LoadMember(string file)
        {
            string[] segments = file.Split('_');
            string name = segments[segments.Length - 1];
            name = name.Replace(".txt", string.Empty);

            string publickeyMainchain = File.ReadAllText(Path.Combine(this.folder, $"PUBLIC_mainchain_{name}.txt"));
            string publickeySidechain = File.ReadAllText(Path.Combine(this.folder, $"PUBLIC_sidechain_{name}.txt"));

            return new FederationMember(name, new PubKey(publickeyMainchain), new PubKey(publickeySidechain));
        }

        /// <summary>
        /// Loads the redeem script for the specified chain.
        /// </summary>
        /// <param name="chain">Mainchain or sidechain.</param>
        /// <returns>The redeem script.</returns>
        public string ReadScriptPubKey(Chain chain)
        {
            string filename = Path.Combine(this.folder, $"{chain.ToString()}_ScriptPubKey.txt");
            if (File.Exists(filename))
            {
                string scriptPubKey = File.ReadAllText(filename);
                return scriptPubKey;
            }
            return string.Empty;
        }

        /// <summary>
        /// Reads the multi-sig address for the specified chain.
        /// </summary>
        /// <param name="chain">Chain to read the multi-sig address for (sidechain or mainchain).</param>
        /// <returns>The address.</returns>
        public string ReadAddress(Chain chain)
        {
            string filename = Path.Combine(this.folder, $"{chain.ToString()}_Address.txt");
            if (File.Exists(filename))
            {
                string address = File.ReadAllText(filename);
                return address;
            }
            return string.Empty;
        }

        /// <summary>
        /// Outputs the Redeem script and the multi-sig address.  Needs all the federation public keys.
        /// </summary>
        /// <param name="federation">The federation.</param>
        /// <param name="chain">The chain to output keys for.</param>
        /// <param name="network">The network used for the generation.</param>
        public void OutputScriptPubKeyAndAddress(IFederation federation, Network network)
        {
            var chain = network.ToChain();

            var script = federation.GenerateScriptPubkey(chain);
            var address = script.Hash.GetAddress(network);

            File.WriteAllText( Path.Combine(this.folder, $"{chain.ToString()}_ScriptPubKey.txt"), script.ToHex() );
            File.WriteAllText( Path.Combine(this.folder, $"{chain.ToString()}_Address.txt"), address.ToString() );
        }

        /// <summary>
        /// Load a federation from data in the folder.
        /// </summary>
        /// <param name="n">Number of members in the federation.</param>
        /// <param name="m">Number of members required for a quorum.</param>
        /// <returns>The loaded federation.</returns>
        public IFederation LoadFederation(int m, int n)
        {
            var members = LoadMembers();
            return new Federation(m, n, members);
        }
    }
}
