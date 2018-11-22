using System;

using NBitcoin;
using NBitcoin.DataEncoders;

using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

using Xunit;
using Xunit.Abstractions;

namespace FedKeyPairGen
{
    public class MultisigAddressCreator
    {
        private readonly ITestOutputHelper output;

        public MultisigAddressCreator(ITestOutputHelper output)
        {
            this.output = output;
        }

        //[Fact]
        [Fact(Skip = "This is not a test, it is meant to be run upon creating a network")]
        public void Run_CreateMultisigAddresses()
        {
            var mainchainNetwork = Networks.Stratis.Testnet();
            var sidechainNetwork = FederatedPegNetwork.NetworksSelector.Testnet();

            this.CreateMultisigAddresses(mainchainNetwork, sidechainNetwork);
        }

        public void CreateMultisigAddresses(Network mainchainNetwork, Network sidechainNetwork, int quorum = 2, int keysCount = 5)
        {
            PubKey[] pubKeys = new PubKey[keysCount];

            for (int i = 0; i < keysCount; i++)
            {
                string password = "mypassword";

                // Create a mnemonic and get the corresponding pubKey.
                Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;
                pubKeys[i] = pubKey;

                this.output.WriteLine($"Mnemonic - Please note the following 12 words down in a secure place: {string.Join(" ", mnemonic.Words)}");
                this.output.WriteLine($"PubKey   - Please share the following public key with the person responsible for the sidechain generation: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                this.output.WriteLine(Environment.NewLine);
            }

            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(quorum, pubKeys);
            this.output.WriteLine("Redeem script: " + payToMultiSig.ToString());

            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(sidechainNetwork);
            this.output.WriteLine("Sidechan P2SH: " + sidechainMultisigAddress.ScriptPubKey);
            this.output.WriteLine("Sidechain Multisig address: " + sidechainMultisigAddress);

            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(mainchainNetwork);
            this.output.WriteLine("Mainchain P2SH: " + mainchainMultisigAddress.ScriptPubKey);
            this.output.WriteLine("Mainchain Multisig address: " + mainchainMultisigAddress);
        }
    }
}