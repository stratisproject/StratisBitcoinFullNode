using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.IntegrationTests.Tools.FederatedNetworkScripts
{
    public abstract class FederatedNetworkScriptBase<TMainNetwork, TSideNetwork> where TMainNetwork : Network where TSideNetwork : Network
    {
        private bool initialized;
        private StringBuilder stringBuilder;

        private List<string> consoleColors;

        protected readonly TMainNetwork mainchainNetwork;
        protected readonly TSideNetwork sidechainNetwork;
        protected int federationMembersCount;
        protected IList<Mnemonic> mnemonics;
        protected (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;

        protected Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;

        protected List<string> mainFederationIps = new List<string>();
        protected List<string> sideFederationIps = new List<string>();

        protected List<NodeSetup> configuredGatewayNodes = new List<NodeSetup>();
        protected List<NodeSetup> configuredUserNodes = new List<NodeSetup>();

        public FederatedNetworkScriptBase(TMainNetwork mainchainNetwork, TSideNetwork sidechainNetwork)
        {
            this.mainchainNetwork = Guard.NotNull(mainchainNetwork, nameof(mainchainNetwork));
            this.sidechainNetwork = Guard.NotNull(sidechainNetwork, nameof(sidechainNetwork));

            this.consoleColors = new List<string>() { "0E", "0A", "09", "0C", "0D" };

            this.initialized = false;
        }

        /// <summary>
        /// Builds the script output, calling a succession of Append or AppendLine instruction.
        /// </summary>
        protected abstract void BuildScript();

        protected void Initialize(IList<Mnemonic> mnemonics, int federationMembersCount)
        {
            Guard.NotNull(mnemonics, nameof(mnemonics));

            this.mnemonics = mnemonics;
            this.pubKeysByMnemonic = mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = this.GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMembersCount = federationMembersCount;

            this.BuildFederationIps();

            this.initialized = true;
        }

        protected virtual void BuildFederationIps()
        {
            var indexes = Enumerable.Range(1, this.federationMembersCount).ToList();

            this.mainFederationIps = indexes.Select(i => $"127.0.0.1:361{i:00}").ToList();
            this.sideFederationIps = indexes.Select(i => $"127.0.0.1:362{i:00}").ToList();
        }

        protected (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }

        protected virtual string GetPortNumberSuffix(NodeType nodeType, int memberIndex)
        {
            switch (nodeType)
            {
                case NodeType.GatewayMain:
                case NodeType.UserMain:
                    return $"1{memberIndex + 1:00}";
                case NodeType.GatewaySide:
                case NodeType.UserSide:
                    return $"2{memberIndex + 1:00}";
                default:
                    throw new ArgumentException("Unknown NodeType");
            }
        }

        protected string GetDataDirFullPath(NodeSetup nodeSetup)
        {
            Network networkReference;
            if (nodeSetup.NodeType == NodeType.GatewayMain || nodeSetup.NodeType == NodeType.UserMain)
                networkReference = this.mainchainNetwork;
            else
                networkReference = this.sidechainNetwork;

            return $"{nodeSetup.DataDir}\\{networkReference.RootFolderName}\\{networkReference.Name}";
        }

        protected string GetConsoleColor(int index)
        {
            return this.consoleColors[index % (this.consoleColors.Count - 1)];
        }

        /// <summary>
        /// Appends the text to the script output, with a final line terminator.
        /// </summary>
        /// <param name="text">The text.</param>
        protected void AppendLine(string text)
        {
            this.stringBuilder.AppendLine(text);
        }

        /// <summary>
        /// Appends the specified text to the script output
        /// </summary>
        /// <param name="text">The text.</param>
        protected void Append(string text)
        {
            this.stringBuilder.Append(text);
        }

        protected void AppendResource(string resourcePath)
        {
            this.AppendLine(this.GetResource(resourcePath));
            this.AppendLine(Environment.NewLine);
        }

        protected string GetResource(string resourcePath)
        {
            try
            {
                using (Stream stream = this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream(resourcePath))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot load resource {resourcePath}");
                throw;
            }
        }

        public string GenerateScript()
        {
            if (!this.initialized)
            {
                throw new Exception("Before generating a script, need to invoke Initialize()");
            }

            this.stringBuilder = new StringBuilder();
            this.BuildScript();

            string result = this.stringBuilder.ToString();
            Guard.Assert(result.Length > 0);

            return result;
        }
    }
}
