using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
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
            Console.WriteLine(redeemScriptRaw);
            if (redeemScriptRaw == null)
                throw new ConfigurationException($"could not find {RedeemScriptParam} configuration parameter");
            this.RedeemScript = new Script(redeemScriptRaw);
            this.MultiSigAddress = RedeemScript.Hash.GetAddress(nodeSettings.Network);
            var payToMultisigScriptParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(nodeSettings.Network, this.RedeemScript);
            this.MultiSigM = payToMultisigScriptParams.SignatureCount;
            this.MultiSigN = payToMultisigScriptParams.PubKeys.Length;
            this.FederationPublicKeys = payToMultisigScriptParams.PubKeys;

            this.PublicKey = configReader.GetOrDefault<string>("publickey", null);

            if (this.FederationPublicKeys.All(p => p != new PubKey(this.PublicKey)))
            {
                throw new ConfigurationException("Please make sure the public key passed as parameter was used to generate the multisig redeem script.");
            }

            this.CounterChainApiPort = configReader.GetOrDefault("counterchainapiport", 0);
            this.FederationNodeIpEndPoints = configReader.GetOrDefault<string>("federationips", null)?.Split(',').Select(a => a.ToIPEndPoint(nodeSettings.Network.DefaultPort));
        }

        public IEnumerable<IPEndPoint> FederationNodeIpEndPoints { get; set; }


        public string PublicKey { get; set; }

        public PubKey[] FederationPublicKeys { get; set; }
 
        public int CounterChainApiPort { get; set; }

        /// <summary>
        /// For the M of N multisig, this is the number of signers required to reach a quorum.
        /// </summary>
        public int MultiSigM { get; set; }

        /// <summary>
        /// For the M of N multisig, this is the number of members in the federation.
        /// </summary>
        public int MultiSigN { get; set; }

        public BitcoinAddress MultiSigAddress { get; set; }

        /// <summary>
        /// Pay2Multisig redeem script.
        /// </summary>
        public Script RedeemScript { get; set; }
    }
}