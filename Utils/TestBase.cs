using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using FluentAssertions;
    using Flurl;
    using Flurl.Http;
    using NBitcoin;
    using Stratis.Bitcoin.IntegrationTests.Common;
    using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
    using Stratis.Bitcoin.Networks;
    using Stratis.FederatedPeg.Features.FederationGateway;
    using Stratis.FederatedPeg.Features.FederationGateway.Models;
    using Stratis.Sidechains.Networks;

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
            this.mainchainNetwork = Networks.Stratis.Regtest();
            this.sidechainNetwork = (FederatedPegRegTest)FederatedPegNetwork.NetworksSelector.Regtest();

            this.mnemonics = this.sidechainNetwork.FederationMnemonics;
            this.pubKeysByMnemonic = this.mnemonics.ToDictionary(m => m, m => m.DeriveExtKey().PrivateKey.PubKey);

            this.scriptAndAddresses = FederatedPegTestHelper.GenerateScriptAndAddresses(this.mainchainNetwork, this.sidechainNetwork, 2, this.pubKeysByMnemonic);

            this.federationMemberIndexes = Enumerable.Range(0, this.pubKeysByMnemonic.Count).ToList();
            this.chains = new[] { "mainchain", "sidechain" }.ToList();
        }
    }
}
