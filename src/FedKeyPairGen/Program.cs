using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace FedKeyPairGen
{
    /*
        Stratis Federation KeyPair Generator v1.0.0.0 - Generates cryptographic key pairs for Sidechain Federation Members.
        Copyright(c) 2018 Stratis Group Limited

        usage:  fedkeypairgen [-name=<name>] [-folder=<output_folder>] [-pass=<password>] [-h]
         -h        This help message.

        Example:  fedkeypairgen 
    */

    // The Stratis Federation KeyPair Generator is a console app that can be sent to Federation Members
    // in order to generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repo.

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Start with the banner.
                FedKeyPairGenManager.OutputHeader();

                bool help = args.Contains("-h");

                // Help command output the usage and examples text.
                if (help)
                {
                    FedKeyPairGenManager.OutputUsage();
                   
                }

                Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

                Console.WriteLine($"-- Mnemonic --");
                Console.WriteLine($"Please keep the following 12 words for yourself and note them down in a secure place:");
                Console.WriteLine($"{string.Join(" ", mnemonic.Words)}");
                Console.WriteLine();
                Console.WriteLine($"-- To share with the sidechain generator --");
                Console.WriteLine($"1. Your pubkey: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                Console.WriteLine($"2. Your ip address: if you're willing to. This is required to help the nodes connect when bootstrapping the network.");
                Console.WriteLine();

                // Write success message including warnings to keep secret private keys safe.
                FedKeyPairGenManager.OutputSuccess();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                FedKeyPairGenManager.OutputErrorLine($"An error occurred: {ex.Message}");
                Console.WriteLine();
                FedKeyPairGenManager.OutputUsage();
            }
        }

        public void MineGenesisBlocks()
        {
            string coinbaseText = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";

            Console.WriteLine("Looking for genesis blocks  for the 3 networks, this might take a while.");

            Block genesisMain = Network.MineGenesisBlock(new PosConsensusFactory(), coinbaseText, new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Coins(50m));
            BlockHeader headerMain = genesisMain.Header;

            Console.WriteLine("-- MainNet network --");
            Console.WriteLine("bits: " + headerMain.Bits);
            Console.WriteLine("nonce: " + headerMain.Nonce);
            Console.WriteLine("time: " + headerMain.Time);
            Console.WriteLine("version: " + headerMain.Version);
            Console.WriteLine("hash: " + headerMain.GetHash());
            Console.WriteLine("merkleroot: " + headerMain.HashMerkleRoot);

            Block genesisTest = Network.MineGenesisBlock(new PosConsensusFactory(), coinbaseText, new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000")), Money.Coins(50m));
            BlockHeader headerTest = genesisTest.Header;
            Console.WriteLine("-- TestNet network --");
            Console.WriteLine("bits: " + headerTest.Bits);
            Console.WriteLine("nonce: " + headerTest.Nonce);
            Console.WriteLine("time: " + headerTest.Time);
            Console.WriteLine("version: " + headerTest.Version);
            Console.WriteLine("hash: " + headerTest.GetHash());
            Console.WriteLine("merkleroot: " + headerTest.HashMerkleRoot);

            Block genesisReg = Network.MineGenesisBlock(new PosConsensusFactory(), coinbaseText, new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), Money.Coins(50m));
            BlockHeader headerReg = genesisReg.Header;
            Console.WriteLine("-- RegTest network --");
            Console.WriteLine("bits: " + headerReg.Bits);
            Console.WriteLine("nonce: " + headerReg.Nonce);
            Console.WriteLine("time: " + headerReg.Time);
            Console.WriteLine("version: " + headerReg.Version);
            Console.WriteLine("hash: " + headerReg.GetHash());
            Console.WriteLine("merkleroot: " + headerReg.HashMerkleRoot);

        }

        public void CreateMultisigAddresses()
        {
            // The following creates 2 members and creates 2-of-5 multisig addresses for both StratisTest and ApexTest.
            int pubKeysCount = 5;
            int mComponent = 2;

            PubKey[] pubKeys = new PubKey[pubKeysCount];

            for (int i = 0; i < pubKeysCount; i++)
            {
                string password = "mypassword";

                // Create a mnemonic and get the corresponding pubKey.
                Mnemonic mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;
                pubKeys[i] = pubKey;
                
                Console.WriteLine($"Mnemonic - Please note the following 12 words down in a secure place: {string.Join(" ", mnemonic.Words)}");
                Console.WriteLine($"PubKey   - Please share the following public key with the person responsible for the sidechain generation: {Encoders.Hex.EncodeData((pubKey).ToBytes(false))}");
                Console.WriteLine(Environment.NewLine);
            }

            Script payToMultiSig = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(mComponent, pubKeys);
            Console.WriteLine("Redeem script: " + payToMultiSig.ToString());

            BitcoinAddress sidechainMultisigAddress = payToMultiSig.Hash.GetAddress(ApexNetwork.Test);
            Console.WriteLine("Sidechan P2SH: " + sidechainMultisigAddress.ScriptPubKey);
            Console.WriteLine("Sidechain Multisig address: " + sidechainMultisigAddress);

            BitcoinAddress mainchainMultisigAddress = payToMultiSig.Hash.GetAddress(new StratisTest());
            Console.WriteLine("Mainchain P2SH: " + mainchainMultisigAddress.ScriptPubKey);
            Console.WriteLine("Mainchain Multisig address: " + mainchainMultisigAddress);
        }
    }
}
