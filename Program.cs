using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
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
            Console.SetIn(new StreamReader(Console.OpenStandardInput(),
                Console.InputEncoding,
                false,
                bufferSize: 1024));

            // Start with the banner and the help message.
            FederationSetup.OutputHeader();
            FederationSetup.OutputMenu();
            

            while (true)
            {
                try
                {
                    Console.Write("Your choice: ");
                    string userInput = Console.ReadLine().Trim();
                    
                    string command = null;
                    if (!string.IsNullOrEmpty(userInput))
                    {
                        args = userInput.Split(" ");
                        command = args[0];
                    }
                    else
                    {
                        args = null;
                        command = null;
                    }
                    
                    Console.WriteLine();

                    if (command == SwitchExit) return;

                    if (command == SwitchMenu)
                    {
                        if (args.Length != 1)
                            throw new ArgumentException("Please enter the exact number of argument required.");

                        FederationSetup.OutputMenu();
                    }

                    if (command == SwitchMineGenesisBlock)
                    {
                        int index = userInput.IndexOf("text=");
                        if (index < 0)
                            throw new ArgumentException("The -text=\"<text>\" argument is missing.");

                        string text = userInput.Substring(userInput.IndexOf("text=") + 5);

                        if (text.Substring(0, 1) != "\"" || text.Substring(text.Length - 1, 1) != "\"")
                            throw new ArgumentException("The -text=\"<text>\" argument should have double-quotes.");
                        
                        text = text.Substring(1, text.Length - 2);

                        if (string.IsNullOrEmpty(text))
                            throw new ArgumentException("Please specify the text to be included in the genesis block.");

                        Console.WriteLine(new GenesisMiner().MineGenesisBlocks(new SmartContractPoAConsensusFactory(), text));
                        FederationSetup.OutputSuccess();
                    }

                    if (command == SwitchGenerateFedPublicPrivateKeys)
                    {
                        if (args.Length != 1)
                            throw new ArgumentException("Please enter the exact number of argument required.");

                        GeneratePublicPrivateKeys();
                        FederationSetup.OutputSuccess();
                    }

                    if (command == SwitchGenerateMultiSigAddresses)
                    {
                        if (args.Length != 4)
                            throw new ArgumentException("Please enter the exact number of argument required.");

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
                }
                catch (Exception ex)
                {
                    FederationSetup.OutputErrorLine($"An error occurred: {ex.Message}");
                    Console.WriteLine();
                    FederationSetup.OutputMenu();
                }
            }
        }

        private static void GeneratePublicPrivateKeys()
        {
            // Generate keys for signing.
            var mnemonicForSigningKey = new Mnemonic(Wordlist.English, WordCount.Twelve);
            PubKey signingPubKey = mnemonicForSigningKey.DeriveExtKey().PrivateKey.PubKey;

            // Generate keys for migning.
            var tool = new KeyTool(new DataFolder(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)));
            Key key = tool.GeneratePrivateKey();

            string savePath = tool.GetPrivateKeySavePath();
            tool.SavePrivateKey(key);
            PubKey miningPubKey = key.PubKey;

            Console.WriteLine($"-----------------------------------------------------------------------------");
            Console.WriteLine($"-- Please give the following 2 public keys to the federation administrator --");
            Console.WriteLine($"-----------------------------------------------------------------------------");
            Console.WriteLine($"1. Your signing pubkey: {Encoders.Hex.EncodeData(signingPubKey.ToBytes(false))}");
            Console.WriteLine($"2. Your mining pubkey: {Encoders.Hex.EncodeData(miningPubKey.ToBytes(false))}");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"------------------------------------------------------------------------------------------");
            Console.WriteLine($"-- Please keep the following 12 words for yourself and note them down in a secure place --");
            Console.WriteLine($"------------------------------------------------------------------------------------------");
            Console.WriteLine($"Your signing mnemonic: {string.Join(" ", mnemonicForSigningKey.Words)}");
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"------------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"-- Please save the following file in a secure place, you'll need it when the federation has been created. --");
            Console.WriteLine($"------------------------------------------------------------------------------------------------------------");
            Console.WriteLine($"File path: {savePath}");
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
            string network = ConfigReader.GetOrDefault("network", (string)null);

            if (string.IsNullOrEmpty(network))
                throw new ArgumentException("Please specify a network.");

            Network mainchainNetwork, sideChainNetwork;
            switch (network)
            {
                case "mainnet":
                    mainchainNetwork = Networks.Stratis.Mainnet();
                    sideChainNetwork = FederatedPegNetwork.NetworksSelector.Mainnet();
                    break;
                case "testnet":
                    mainchainNetwork = Networks.Stratis.Testnet();
                    sideChainNetwork = FederatedPegNetwork.NetworksSelector.Testnet();
                    break;
                case "regtest":
                    mainchainNetwork = Networks.Stratis.Regtest();
                    sideChainNetwork = FederatedPegNetwork.NetworksSelector.Regtest();
                    break;
                default:
                    throw new ArgumentException("Please specify a network such as: mainnet, testnet or regtest.");

            }

            return (mainchainNetwork, sideChainNetwork);
        }
    }
}
