using System;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Configuration settings used to initialize a FederationGateway.
    /// </summary>
    public sealed class FederationGatewaySettings
    {
        /// <summary>
        /// The MemberName is used to distiguish between federation gateways in the debug logs.
        /// </summary>
        public string MemberName { get; set; }

        /// <summary>
        /// A string representation of the PublicKey used for determining turns in the round robin.
        /// </summary>
        public string PublicKey { get; set; }

        /// <summary>
        /// Path to the public keys of the federation members.
        /// </summary>
        public string FederationFolder { get; set; }

        /// <summary>
        /// Path to the folder containing the private key this node uses.  Used for signing multi-sig transactions.
        /// </summary>
        public string MemberPrivateFolder { get; set; }

        /// <summary>
        /// The API port of the counterchain.  <example>The federation members are required to run full nodes for both the
        /// sidechain and the mainchain.  If this is the mainchain then the CounterChainApiPort is the api port of the sidechain node.</example>
        /// </summary>
        public int CounterChainApiPort { get; set; }

        /// <summary>
        /// For the M of N multisig, this is the number of signers required to reach a quorum.
        /// </summary>
        public int MultiSigM { get; set; }

        /// <summary>
        /// For the M of N multisig, this is the number of members in the federation.
        /// </summary>
        public int MultiSigN { get; set; }

        /// <summary>
        /// The name of the multisig wallet used for the multisig transactions.
        /// </summary>
        public string MultiSigWalletName { get; set; }

        /// <summary>
        /// A callback allow changing the settings after they have been read from the nodeSettings.
        /// </summary>
        private readonly Action<FederationGatewaySettings> callback;

        /// <summary>
        /// Initialize the settings with an optional callback which can be used to change the settings after they have been read from the config.
        /// </summary>
        /// <param name="callback">This optional callback is called after the settings are read from the config allowing the settings to be changed in code.</param>
        public FederationGatewaySettings(Action<FederationGatewaySettings> callback = null)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads our feature specific settings from the nodeSettings' ConfigReader. 
        /// </summary>
        /// <param name="nodeSettings"></param>
        public void Load(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.MemberName = config.GetOrDefault("membername", "unspecified");
            this.MultiSigM = config.GetOrDefault("multisigM", 0);
            this.MultiSigN = config.GetOrDefault("multisigN", 0);
            this.MultiSigWalletName = config.GetOrDefault("multisigwalletname", "multisig_wallet");
            this.PublicKey = config.GetOrDefault<string>("publickey", null);
            this.FederationFolder = config.GetOrDefault<string>("federationfolder", null);
            this.MemberPrivateFolder = config.GetOrDefault<string>("memberprivatefolder", null);
            this.CounterChainApiPort = config.GetOrDefault("counterchainapiport", 0);
            this.callback?.Invoke(this);
        }
    }
}