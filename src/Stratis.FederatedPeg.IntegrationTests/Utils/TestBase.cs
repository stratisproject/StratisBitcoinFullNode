using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    public class TestBase
    {
        protected readonly Network mainchainNetwork;

        protected readonly FederatedPegRegTest sidechainNetwork;

        protected readonly IList<Mnemonic> mnemonics;

        protected readonly Dictionary<Mnemonic, PubKey> pubKeysByMnemonic;

        protected readonly (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress) scriptAndAddresses;

        protected readonly List<int> federationMemberIndexes;

        protected readonly List<string> chains;

        public TestBase()
        {
            this.mainchainNetwork = Networks.Stratis.Testnet();
            this.sidechainNetwork = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            this.mnemonics = this.sidechainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = this.GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMemberIndexes = Enumerable.Range(0, this.pubKeysByMnemonic.Count).ToList();
            this.chains = new[] { "mainchain", "sidechain" }.ToList();
        }

        protected (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }
    }
}
