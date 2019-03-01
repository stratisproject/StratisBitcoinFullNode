using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg
{
    /// <inheritdoc />
    public sealed class FederationGatewaySettings : IFederationGatewaySettings
    {
        internal const string CounterChainApiPortParam = "counterchainapiport";

        internal const string RedeemScriptParam = "redeemscript";

        internal const string PublicKeyParam = "publickey";

        internal const string FederationIpsParam = "federationips";

        private const string MinCoinMaturityParam = "mincoinmaturity";

        private const string MinimumDepositConfirmationsParam = "mindepositconfirmations";

        private const string TransactionFeeParam = "transactionfee";

        public FederationGatewaySettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration configReader = nodeSettings.ConfigReader;

            this.IsMainChain = configReader.GetOrDefault<bool>("mainchain", false);
            if (!this.IsMainChain && !configReader.GetOrDefault("sidechain", false))
                throw new ConfigurationException("Either -mainchain or -sidechain must be specified");

            string redeemScriptRaw = configReader.GetOrDefault<string>(RedeemScriptParam, null);
            Console.WriteLine(redeemScriptRaw);
            if (redeemScriptRaw == null)
                throw new ConfigurationException($"could not find {RedeemScriptParam} configuration parameter");
            this.MultiSigRedeemScript = new Script(redeemScriptRaw);
            this.MultiSigAddress = this.MultiSigRedeemScript.Hash.GetAddress(nodeSettings.Network);
            PayToMultiSigTemplateParameters payToMultisigScriptParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(this.MultiSigRedeemScript);
            this.MultiSigM = payToMultisigScriptParams.SignatureCount;
            this.MultiSigN = payToMultisigScriptParams.PubKeys.Length;
            this.FederationPublicKeys = payToMultisigScriptParams.PubKeys;

            this.PublicKey = configReader.GetOrDefault<string>(PublicKeyParam, null);
            this.MinCoinMaturity = configReader.GetOrDefault<int>(MinCoinMaturityParam, (int)nodeSettings.Network.Consensus.MaxReorgLength + 1);
            if (this.MinCoinMaturity <= 0)
            {
                throw new ConfigurationException("The minimum coin maturity can't be set to zero or less.");
            }

            this.TransactionFee = new Money(configReader.GetOrDefault<decimal>(TransactionFeeParam, 0.01m), MoneyUnit.BTC);

            if (this.FederationPublicKeys.All(p => p != new PubKey(this.PublicKey)))
            {
                throw new ConfigurationException("Please make sure the public key passed as parameter was used to generate the multisig redeem script.");
            }

            this.CounterChainApiPort = configReader.GetOrDefault(CounterChainApiPortParam, 0);
            this.FederationNodeIpEndPoints = configReader.GetOrDefault<string>(FederationIpsParam, null)?.Split(',')
                .Select(a => a.ToIPEndPoint(nodeSettings.Network.DefaultPort)) ?? new List<IPEndPoint>();

            //todo : remove that for prod code
            this.MinimumDepositConfirmations = (uint)configReader.GetOrDefault<int>(MinimumDepositConfirmationsParam, (int)nodeSettings.Network.Consensus.MaxReorgLength + 1);
            //this.MinimumDepositConfirmations = nodeSettings.Network.Consensus.MaxReorgLength + 1;
        }

        /// <inheritdoc/>
        public bool IsMainChain { get; }

        /// <inheritdoc/>
        public IEnumerable<IPEndPoint> FederationNodeIpEndPoints { get; }

        /// <inheritdoc/>
        public string PublicKey { get; }

        /// <inheritdoc/>
        public PubKey[] FederationPublicKeys { get; }

        /// <inheritdoc/>
        public int CounterChainApiPort { get; }

        /// <inheritdoc/>
        public int MultiSigM { get; }

        /// <inheritdoc/>
        public int MultiSigN { get; }

        /// <inheritdoc/>
        public int MinCoinMaturity { get; }

        /// <inheritdoc/>
        public Money TransactionFee { get; }

        /// <inheritdoc/>
        public BitcoinAddress MultiSigAddress { get; }

        /// <inheritdoc/>
        public Script MultiSigRedeemScript { get; }

        /// <inheritdoc />
        public uint MinimumDepositConfirmations { get; }
    }
}