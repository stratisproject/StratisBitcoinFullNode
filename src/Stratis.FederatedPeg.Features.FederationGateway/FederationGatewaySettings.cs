using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Configuration settings used to initialize a FederationGateway.
    /// </summary>
    public sealed class FederationGatewaySettings
    {
        private const string RedeemScriptParam = "redeemscript";

        public FederationGatewaySettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            var configReader = nodeSettings.ConfigReader;
            var redeemScriptRaw = configReader.GetOrDefault<string>(RedeemScriptParam, null);
            if (redeemScriptRaw == null)
                throw new ConfigurationException($"could not find {RedeemScriptParam} configuration parameter");

            this.RedeemScript = new Script(redeemScriptRaw);
            this.MultiSigAddress = RedeemScript.Hash.GetAddress(nodeSettings.Network);
            var payToMultisigScriptParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(
                nodeSettings.Network, this.RedeemScript);
            this.MultiSigM = configReader.GetOrDefault("multisigM", payToMultisigScriptParams.SignatureCount);
            this.MultiSigN = configReader.GetOrDefault("multisigN", payToMultisigScriptParams.PubKeys.Length);

            this.MemberName = configReader.GetOrDefault("membername", "unspecified");

            this.MultiSigWalletName = configReader.GetOrDefault("multisigwalletname", "multisig_wallet");
            this.PublicKey = configReader.GetOrDefault<string>("publickey", null);
            this.FederationFolder = configReader.GetOrDefault<string>("federationfolder", null);
            this.MemberPrivateFolder = configReader.GetOrDefault<string>("memberprivatefolder", null);
            this.CounterChainApiPort = configReader.GetOrDefault("counterchainapiport", 0);
           
            this.FederationNodeIps = configReader.GetOrDefault<string>("federationips", null)?.Split(',').Select(a => a.ToIPEndPoint(nodeSettings.Network.DefaultPort));
        }

        public IEnumerable<IPEndPoint> FederationNodeIps { get; set; }

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
        /// Mutlisig bitcoin address.
        /// </summary>
        public BitcoinAddress MultiSigAddress { get; set; }

        /// <summary>
        /// Pay2Multisig redeem script.
        /// </summary>
        public Script RedeemScript { get; set; }
    }
}