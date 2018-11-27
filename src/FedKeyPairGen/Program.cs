using System;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace FederationSetup
{
    /*
        Stratis Federation set up v1.0.0.0 - Set-up genesis block, multisig addresses and generates cryptographic key pairs for Sidechain Federation Members.
        Copyright(c) 2018 Stratis Group Limited

        usage:  federationsetup [-h]
         -h        This help message.

        Example:  federationsetup -g -a -p
    */

    // The Stratis Federation set-up is a console app that can be sent to Federation Members
    // in order to set-up the network and generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repository.

    class Program
    {
        private const string SwitchHelp = "-h";
        private const string SwitchMineGenesisBlock = "-g";
        private const string SwitchGenerateFedPublicPrivateKeys = "-p";
        private const string SwitchGenerateMultiSigAddresses = "-m";

        private static TextFileConfiguration ConfigReader;

        static void Main(string[] args)
        {
            try
            {
                ConfigReader = new TextFileConfiguration(args ?? new string[] { });

                // Start with the banner.
                FederationSetup.OutputHeader();

                // Help command output the usage and examples text.
                if (args.Contains(SwitchHelp))
                {
                    FederationSetup.OutputUsage();
                }

                if (args.Contains(SwitchMineGenesisBlock))
                {
                    Console.WriteLine(new GenesisMiner().MineGenesisBlocks(
                        new PoAConsensusFactory(),
                        "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/"));
                }

                if (args.Contains(SwitchGenerateFedPublicPrivateKeys))
                {
                    GeneratePublicPrivateKeys();
                }

                if (args.Contains(SwitchGenerateMultiSigAddresses))
                {
                    string[] federatedPublicKeys = GetFederatedPublicKeysFromArguments();

                    int quorum = GetQuorumFromArguments(federatedPublicKeys.Length);

                    (Network mainChain, Network sideChain) = GetMainAndSideChainNetworksFromArguments();

                    Console.WriteLine(new MultisigAddressCreator().CreateMultisigAddresses(
                        mainChain, sideChain, federatedPublicKeys.Select(f => new PubKey(f)).ToArray(), quorum));
                }
            }
            catch (Exception ex)
            {
                FederationSetup.OutputErrorLine($"An error occurred: {ex.Message}");
                Console.WriteLine();
                FederationSetup.OutputUsage();
            }
            finally
            {
                Console.ReadLine();
            }
        }

        private static void GeneratePublicPrivateKeys()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

            Console.WriteLine($"-- For Sidechain Generator --");
            Console.WriteLine($"-----------------------------");
            Console.WriteLine($"-- Mnemonic --");
            Console.WriteLine($"Please keep the following 12 words for yourself and note them down in a secure place:");
            Console.WriteLine($"{string.Join(" ", mnemonic.Words)}");
            Console.WriteLine();
            Console.WriteLine($"-- To share with the sidechain generator --");
            Console.WriteLine($"1. Your pubkey: {Encoders.Hex.EncodeData(pubKey.ToBytes(false))}");
            Console.WriteLine($"2. Your ip address: if you're willing to. This is required to help the nodes connect when bootstrapping the network.");
            Console.WriteLine(Environment.NewLine);

            mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

            Console.WriteLine($"-- For Sidechain Mining --");
            Console.WriteLine($"--------------------------");
            Console.WriteLine($"-- Mnemonic --");
            Console.WriteLine($"Please keep the following 12 words for yourself and note them down in a secure place:");
            Console.WriteLine($"{string.Join(" ", mnemonic.Words)}");
            Console.WriteLine();
            Console.WriteLine($"-- To share for Sidechain Mining --");
            Console.WriteLine($"1. Your pubkey: {Encoders.Hex.EncodeData(pubKey.ToBytes(false))}");
            Console.WriteLine(Environment.NewLine);

            // Write success message including warnings to keep secret private keys safe.
            FederationSetup.OutputSuccess();
        }

        private static int GetQuorumFromArguments(int federatedPublicKeysCount)
        {
            int quorum = ConfigReader.GetOrDefault("quorum", 3);

            if (quorum < federatedPublicKeysCount / 2)
                throw new ArgumentException("Quorum has to be greater than half of the members within the federation.", "-m -quorum");

            return quorum;
        }

        private static string[] GetFederatedPublicKeysFromArguments()
        {
            string[] pubKeys = null;

            int federatedPublicKeyCount = 0;

            if (ConfigReader.GetAll("keys").FirstOrDefault() != null)
            {
                pubKeys = ConfigReader.GetAll("keys").FirstOrDefault().Split(',');
                federatedPublicKeyCount = pubKeys.Count();
            }

            if (federatedPublicKeyCount == 0)
                throw new ArgumentException("Federated member public keys do not exist.", "-m -keys");

            if (federatedPublicKeyCount % 2 == 0)
                throw new ArgumentException("Federation must have an odd number of members.", "-m -keys");

            if (federatedPublicKeyCount > 15)
                throw new ArgumentException("Federation can only have up to fifteen members.", "-m -keys");

            return pubKeys;
        }

        private static (Network mainChain, Network sideChain) GetMainAndSideChainNetworksFromArguments()
        {
            Network mainchainNetwork = Networks.Stratis.Mainnet();
            Network sideChainNetwork = FederatedPegNetwork.NetworksSelector.Mainnet();

            bool testNet = ConfigReader.GetOrDefault("testnet", false);
            bool regTest = ConfigReader.GetOrDefault("regtest", false);

            mainchainNetwork = testNet ? Networks.Stratis.Testnet() :
                        regTest ? Networks.Stratis.Testnet() :
                        Networks.Stratis.Mainnet();

            sideChainNetwork = testNet ? FederatedPegNetwork.NetworksSelector.Testnet() :
                        regTest ? FederatedPegNetwork.NetworksSelector.Testnet() :
                        FederatedPegNetwork.NetworksSelector.Mainnet();

            return (mainchainNetwork, sideChainNetwork);
        }
    }
}
