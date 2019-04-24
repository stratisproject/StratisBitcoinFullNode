using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public static class FederatedPegTestHelper
    {
        public const string SkipTests = "Currently skipping all sidechains tests until they are stable. Make TestingValues.SkipTests null to prevent skipping.";

        public static (Script payToMultiSig, BitcoinAddress sidechainMultisigAddress, BitcoinAddress mainchainMultisigAddress)
            GenerateScriptAndAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum, Dictionary<Mnemonic, PubKey> pubKeysByMnemonic)
        {
            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeysByMnemonic.Values.ToArray());
            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            return (payToMultiSig, sidechainMultisigAddress, mainchainMultisigAddress);
        }
    }
}