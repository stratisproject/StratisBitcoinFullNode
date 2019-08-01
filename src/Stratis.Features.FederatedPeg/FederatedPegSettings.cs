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
    public interface IFederatedPegOptions
    {
        int WalletSyncFromHeight { get; }
    }

    public sealed class FederatedPegOptions : IFederatedPegOptions
    {
        /// <summary>
        /// The height to start syncing the wallet from.
        /// </summary>
        public int WalletSyncFromHeight { get; }

        public FederatedPegOptions(int walletSyncFromHeight = 1)
        {
            this.WalletSyncFromHeight = walletSyncFromHeight;
        }
    }

    /// <inheritdoc />
    public sealed class FederatedPegSettings : IFederatedPegSettings
    {
        public const string WalletSyncFromHeightParam = "walletsyncfromheight";

        public const string RedeemScriptParam = "redeemscript";

        public const string PublicKeyParam = "publickey";

        public const string FederationIpsParam = "federationips";

        public const string CounterChainDepositBlock = "counterchaindepositblock";

        private const string MinimumDepositConfirmationsParam = "mindepositconfirmations";

        /// <summary>
        /// The fee taken by the federation to build withdrawal transactions. The federation will keep most of this.
        /// </summary>
        /// <remarks>
        /// Changing <see cref="CrossChainTransferFee"/> affects both the deposit threshold on this chain and the withdrawal transaction fee on this chain.
        /// This value shouldn't be different for the 2 pegged chain nodes or deposits could be extracted that don't have the amount required to
        /// cover the withdrawal fee on the other chain.
        ///
        /// TODO: This should be configurable on the Network level in the future, but individual nodes shouldn't be tweaking it.
        /// </remarks>
        public static readonly Money CrossChainTransferFee = Money.Coins(0.001m);

        /// <summary>
        /// Only look for deposits above a certain value. This avoids issues with dust lingering around or fees not being covered.
        /// </summary>
        public static readonly Money CrossChainTransferMinimum = Money.Coins(1m);

        /// <summary>
        /// The fee always given to a withdrawal transaction.
        /// </summary>
        public static readonly Money BaseTransactionFee = Money.Coins(0.0002m);

        /// <summary>
        /// The extra fee given to a withdrawal transaction per input it spends. This number should be high enough such that the built transactions are always valid, yet low enough such that the federation can turn a profit.
        /// </summary>
        public static readonly Money InputTransactionFee = Money.Coins(0.00012m);

        /// <summary>
        /// Fee applied to consolidating transactions.
        /// </summary>
        public static readonly Money ConsolidationFee = Money.Coins(0.01m);

        /// <summary>
        /// The maximum number of inputs we want our built withdrawal transactions to have. We don't want them to get too big for Standardness reasons.
        /// </summary>
        public const int MaxInputs = 50;

        /// <summary>
        /// Sidechains to STRAT don't need to check for deposits for the whole main chain. Only from when they begun.
        ///
        /// This block was mined on 5th Dec 2018. Further optimisations could be more specific per network.
        /// </summary>
        public const int StratisMainDepositStartBlock = 1_100_000;

        public FederatedPegSettings(NodeSettings nodeSettings, IFederatedPegOptions federatedPegOptions = null)
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

            if (this.FederationPublicKeys.All(p => p != new PubKey(this.PublicKey)))
            {
                throw new ConfigurationException("Please make sure the public key passed as parameter was used to generate the multisig redeem script.");
            }

            // Federation IPs - These are required to receive and sign withdrawal transactions.
            string federationIpsRaw = configReader.GetOrDefault<string>(FederationIpsParam, null);

            if (federationIpsRaw == null)
                throw new ConfigurationException("Federation IPs must be specified.");

            IEnumerable<IPEndPoint> endPoints = federationIpsRaw.Split(',').Select(a => a.ToIPEndPoint(nodeSettings.Network.DefaultPort));

            this.FederationNodeIpEndPoints = new HashSet<IPEndPoint>(endPoints, new IPEndPointComparer());
            this.FederationNodeIpAddresses = new HashSet<IPAddress>(endPoints.Select(x=>x.Address), new IPAddressComparer());

            // These values are only configurable for tests at the moment. Fed members on live networks shouldn't play with them.
            this.CounterChainDepositStartBlock = configReader.GetOrDefault<int>(CounterChainDepositBlock, this.IsMainChain ? 1 : StratisMainDepositStartBlock);
            this.MinimumDepositConfirmations = (uint)configReader.GetOrDefault<int>(MinimumDepositConfirmationsParam, (int)nodeSettings.Network.Consensus.MaxReorgLength + 1);
            this.WalletSyncFromHeight = configReader.GetOrDefault(WalletSyncFromHeightParam, federatedPegOptions?.WalletSyncFromHeight ?? 0);
        }

        /// <inheritdoc/>
        public bool IsMainChain { get; }

        /// <inheritdoc/>
        public HashSet<IPEndPoint> FederationNodeIpEndPoints { get; }

        /// <inheritdoc/>
        public HashSet<IPAddress> FederationNodeIpAddresses { get; }

        /// <inheritdoc/>
        public string PublicKey { get; }

        /// <inheritdoc/>
        public PubKey[] FederationPublicKeys { get; }

        /// <inheritdoc/>
        public int WalletSyncFromHeight { get; }

        /// <inheritdoc/>
        public int MultiSigM { get; }

        /// <inheritdoc/>
        public int MultiSigN { get; }

        /// <inheritdoc/>
        public Money GetWithdrawalTransactionFee(int numInputs)
        {
            return BaseTransactionFee + numInputs * InputTransactionFee;
        }

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