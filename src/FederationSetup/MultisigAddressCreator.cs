using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;
using Xunit;
using Xunit.Abstractions;

namespace FederationSetup
{
    public class MultisigAddressCreator
    {
        private readonly ITestOutputHelper output;

        public MultisigAddressCreator(ITestOutputHelper output = null)
        {
            if (output == null) return;
            this.output = output;
        }

        public string CreateMultisigAddresses(Network mainchainNetwork, Network sidechainNetwork, PubKey[] pubKeys, int quorum = 3)
        {
            var output = new StringBuilder();

            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeys);
            output.AppendLine("Redeem script: " + payToMultiSig.ToString());

            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            output.AppendLine("Sidechan P2SH: " + sidechainMultisigAddress.ScriptPubKey);
            output.AppendLine("Sidechain Multisig address: " + sidechainMultisigAddress);

            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            output.AppendLine("Mainchain P2SH: " + mainchainMultisigAddress.ScriptPubKey);
            output.AppendLine("Mainchain Multisig address: " + mainchainMultisigAddress);

            return output.ToString();
        }
    }
}