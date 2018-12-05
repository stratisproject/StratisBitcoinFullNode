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
    // The Stratis Federation set-up is a console app that can be sent to Federation Members
    // in order to set-up the network and generate their Private (and Public) keys without a need to run a Node at this stage.
    // See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the
    // project repository.
    class Program
    {
        private const string SwitchMineGenesisBlock = "g";
        private const string SwitchGenerateFedPublicPrivateKeys = "p";
        private const string SwitchGenerateMultiSigAddresses = "m";
        private const string SwitchMenu = "menu";
        private const string SwitchExit = "exit";

        private static TextFileConfiguration ConfigReader;

        static void Main(string[] args)
        {
            // Start with the banner and the help message.
            FederationSetup.OutputHeader();
            FederationSetup.OutputMenu();
            args = GetChoice();

            while (true)
            {
                try
                {
                    string command = null;
                    if (args != null)
                    {
                        command = args[0];
                    }

                    Console.WriteLine();

                    if (command == SwitchExit) return;

                    if (command == SwitchMenu)
                    {
                        FederationSetup.OutputMenu();
                    }

                    if (command == SwitchMineGenesisBlock)
                    {
                        Console.WriteLine(new GenesisMiner().MineGenesisBlocks(new PoAConsensusFactory(), "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/"));
                        FederationSetup.OutputSuccess();
                    }

                    if (command == SwitchGenerateFedPublicPrivateKeys)
                    {
                        GeneratePublicPrivateKeys();
                        FederationSetup.OutputSuccess();
                    }

                    if (command == SwitchGenerateMultiSigAddresses)
                    {

                        ConfigReader = new TextFileConfiguration(args);

                        int quorum = GetQuorumFromArguments();
                        string[] federatedPublicKeys = GetFederatedPublicKeysFromArguments();

                        if (quorum > federatedPublicKeys.Length)
                            throw new ArgumentException("Quorum has to be smaller than the number of members within the federation.");

                        if (quorum < federatedPublicKeys.Length / 2)
                            throw new ArgumentException("Quorum has to be greater than half of the members within the federation.");

                        (Network mainChain, Network sideChain) = GetMainAndSideChainNetworksFromArguments();

                        Console.WriteLine($"Creating multisig addresses for {mainChain.Name} and {sideChain.Name}.");
                        Console.WriteLine(new MultisigAddressCreator().CreateMultisigAddresses(mainChain, sideChain, federatedPublicKeys.Select(f => new PubKey(f)).ToArray(), quorum));
                    }

                    args = GetChoice();
                }
                catch (Exception ex)
                {
                    FederationSetup.OutputErrorLine($"An error occurred: {ex.Message}");
                    Console.WriteLine();
                    FederationSetup.OutputMenu();
                    args = GetChoice();
                }
            }
        }

        private static string[] GetChoice()
        {
            Console.Write("Your choice: ");
            string command = Console.ReadLine();

            if (!string.IsNullOrEmpty(command))
            {
                return command.Split(" ");
            }

            return null;
        }

        private static void GeneratePublicPrivateKeys()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            PubKey pubKey = mnemonic.DeriveExtKey().PrivateKey.PubKey;

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
        }

        private static int GetQuorumFromArguments()
        {
            int quorum = ConfigReader.GetOrDefault("quorum", 0);

            if (quorum == 0)
                throw new ArgumentException("Please specify a quorum.");

            if (quorum < 0)
                throw new ArgumentException("Please specify a positive number for the quorum.");

            return quorum;
        }

        private static string[] GetFederatedPublicKeysFromArguments()
        {
            string[] pubKeys = null;

            int federatedPublicKeyCount = 0;

            if (ConfigReader.GetAll("fedpubkeys").FirstOrDefault() != null)
            {
                pubKeys = ConfigReader.GetAll("fedpubkeys").FirstOrDefault().Split(',');
                federatedPublicKeyCount = pubKeys.Count();
            }

            if (federatedPublicKeyCount == 0)
                throw new ArgumentException("No federation member public keys specified.");

            if (federatedPublicKeyCount % 2 == 0)
                throw new ArgumentException("The federation must have an odd number of members.");

            if (federatedPublicKeyCount > 15)
                throw new ArgumentException("The federation can only have up to fifteen members.");

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
