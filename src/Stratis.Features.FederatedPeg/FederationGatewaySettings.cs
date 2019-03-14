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
        public const string CounterChainApiPortParam = "counterchainapiport";

        public const string RedeemScriptParam = "redeemscript";

        public const string PublicKeyParam = "publickey";

        public const string FederationIpsParam = "federationips";

        private const string MinimumDepositConfirmationsParam = "mindepositconfirmations";

        private const string TransactionFeeParam = "transactionfee";

        /// <summary>
        /// Sidechains to STRAT don't need to check for deposits for the whole main chain. Only from when they begun.
        ///
        /// This block was mined on 5th Dec 2018. Further optimisations could be more specific per network.
        /// </summary>
        public const int StratisMainDepositStartBlock = 1_100_000;

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

            this.TransactionFee = new Money(configReader.GetOrDefault<decimal>(TransactionFeeParam, 0.01m), MoneyUnit.BTC);

            if (this.FederationPublicKeys.All(p => p != new PubKey(this.PublicKey)))
            {
                throw new ConfigurationException("Please make sure the public key passed as parameter was used to generate the multisig redeem script.");
            }

            this.CounterChainApiPort = configReader.GetOrDefault(CounterChainApiPortParam, 0);

            this.CounterChainDepositStartBlock = this.IsMainChain ? 1 : StratisMainDepositStartBlock;

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
        public Money TransactionFee { get; }

        /// <inheritdoc/>
        public int CounterChainDepositStartBlock { get; }

        /// <inheritdoc/>
        public BitcoinAddress MultiSigAddress { get; }

        /// <inheritdoc/>
        public Script MultiSigRedeemScript { get; }

        /// <inheritdoc />
        public uint MinimumDepositConfirmations { get; }
    }
}