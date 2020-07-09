using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Sidechains.Networks;

namespace FederationSetup
{
    /// <summary>
    /// The Stratis Federation set-up is a console app that can be sent to Federation Members
    /// in order to set-up the network and generate their Private (and Public) keys without a need to run a Node at this stage.
    /// See the "Use Case - Generate Federation Member Key Pairs" located in the Requirements folder in the project repository.
    /// </summary>
    class Program
    {
        private const string SwitchMineGenesisBlock = "g";
        private const string SwitchGenerateFedPublicPrivateKeys = "p";
        private const string SwitchGenerateMultiSigAddresses = "m";
        private const string SwitchGenerateRecoveryTransaction = "r";
        private const string SwitchMenu = "menu";
        private const string SwitchExit = "exit";

        private static TextFileConfiguration ConfigReader;

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                SwitchCommand(args, args[0], string.Join(" ", args));
                return;
            }

            Console.SetIn(new StreamReader(Console.OpenStandardInput(), Console.InputEncoding, false, bufferSize: 1024));

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
                    }

                    Console.WriteLine();

                    SwitchCommand(args, command, userInput);
                }
                catch (Exception ex)
                {
                    FederationSetup.OutputErrorLine($"An error occurred: {ex.Message}");
                    Console.WriteLine();
                    FederationSetup.OutputMenu();
                }
            }
        }

        private static void SwitchCommand(string[] args, string command, string userInput)
        {
            switch (command)
            {
                case SwitchExit:
                {
                   Environment.Exit(0);
                   break;
                }
                case SwitchMenu:
                {
                    HandleSwitchMenuCommand(args);
                    break;
                }
                case SwitchMineGenesisBlock:
                {
                    HandleSwitchMineGenesisBlockCommand(userInput);
                    break;
                }
                case SwitchGenerateFedPublicPrivateKeys:
                {
                    HandleSwitchGenerateFedPublicPrivateKeysCommand(args);
                    break;
                }
                case SwitchGenerateMultiSigAddresses:
                {
                    HandleSwitchGenerateMultiSigAddressesCommand(args);
                    break;
                }
                case SwitchGenerateRecoveryTransaction:
                {
                    HandleSwitchGenerateFundsRecoveryTransaction(args);
                    break;
                }
            }
        }

        private static void HandleSwitchMenuCommand(string[] args)
        {
            if (args.Length != 1)
                throw new ArgumentException("Please enter the exact number of argument required.");

            FederationSetup.OutputMenu();
        }

        private static void HandleSwitchMineGenesisBlockCommand(string userInput)
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

        private static void HandleSwitchGenerateFedPublicPrivateKeysCommand(string[] args)
        {
            if (args.Length != 1 && args.Length != 2 && args.Length != 3 && args.Length != 4)
                throw new ArgumentException("Please enter the exact number of argument required.");

            string passphrase = null;
            string dataDirPath = null;
            string isMultisig = null;

            dataDirPath = Array.Find(args, element =>
                element.StartsWith("-datadir=", StringComparison.Ordinal));

            passphrase = Array.Find(args, element =>
                element.StartsWith("-passphrase=", StringComparison.Ordinal));

            isMultisig = Array.Find(args, element =>
                element.StartsWith("-ismultisig=", StringComparison.Ordinal));

            if (String.IsNullOrEmpty(passphrase))
                throw new ArgumentException("The -passphrase=\"<passphrase>\" argument is missing.");

            passphrase = passphrase.Replace("-passphrase=", string.Empty);

            //ToDo wont allow for datadir with equal sign
            dataDirPath = String.IsNullOrEmpty(dataDirPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : dataDirPath.Replace("-datadir=", String.Empty);

            if (String.IsNullOrEmpty(isMultisig) || isMultisig.Replace("-ismultisig=", String.Empty) == "true")
            {
                GeneratePublicPrivateKeys(passphrase, dataDirPath);
            }
            else
            {
                GeneratePublicPrivateKeys(passphrase, dataDirPath, isMultiSigOutput: false);
            }

            FederationSetup.OutputSuccess();
        }

        private static void ConfirmArguments(TextFileConfiguration config, params string[] args)
        {
            var missing = new Dictionary<string, string>();

            foreach (string arg in args)
            {
                if (config.GetOrDefault<string>(arg, null) == null)
                {
                    Console.Write(arg + ": ");
                    missing[arg] = Console.ReadLine();
                }
            }

            new TextFileConfiguration(missing.Select(d => $"{d.Key}={d.Value}").ToArray()).MergeInto(config);

            Console.WriteLine();
        }

        private static void HandleSwitchGenerateMultiSigAddressesCommand(string[] args)
        {
            ConfigReader = new TextFileConfiguration(args);

            ConfirmArguments(ConfigReader, "network", "quorum", "fedpubkeys");

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

        private static void GeneratePublicPrivateKeys(string passphrase, String keyPath, bool isMultiSigOutput = true)
        {
            // Generate keys for signing.
            var mnemonicForSigningKey = new Mnemonic(Wordlist.English, WordCount.Twelve);
            PubKey signingPubKey = mnemonicForSigningKey.DeriveExtKey(passphrase).PrivateKey.PubKey;

            // Generate keys for migning.
            var tool = new KeyTool(keyPath);

            Key key = tool.GeneratePrivateKey();

            string savePath = tool.GetPrivateKeySavePath();
            tool.SavePrivateKey(key);
            PubKey miningPubKey = key.PubKey;

            Console.WriteLine($"Your Masternode Public Key: {Encoders.Hex.EncodeData(miningPubKey.ToBytes(false))}");
            Console.WriteLine($"-----------------------------------------------------------------------------");

            if (isMultiSigOutput)
            {
                Console.WriteLine(
                    $"Your Masternode Signing Key: {Encoders.Hex.EncodeData(signingPubKey.ToBytes(false))}");
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine(
                    $"------------------------------------------------------------------------------------------");
                Console.WriteLine(
                    $"-- Please keep the following 12 words for yourself and note them down in a secure place --");
                Console.WriteLine(
                    $"------------------------------------------------------------------------------------------");
                Console.WriteLine($"Your signing mnemonic: {string.Join(" ", mnemonicForSigningKey.Words)}");
            }

            if (passphrase != null)
            {
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine($"Your passphrase: {passphrase}");
            }

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
                    sideChainNetwork = CirrusNetwork.NetworksSelector.Mainnet();
                    break;
                case "testnet":
                    mainchainNetwork = Networks.Stratis.Testnet();
                    sideChainNetwork = CirrusNetwork.NetworksSelector.Testnet();
                    break;
                case "regtest":
                    mainchainNetwork = Networks.Stratis.Regtest();
                    sideChainNetwork = CirrusNetwork.NetworksSelector.Regtest();
                    break;
                default:
                    throw new ArgumentException("Please specify a network such as: mainnet, testnet or regtest.");

            }

            return (mainchainNetwork, sideChainNetwork);
        }

        private static string GetDataDirFromArguments()
        {
            return ConfigReader.GetOrDefault<string>("datadir", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        private static Script GetRedeemScriptFromArguments(bool old)
        {
            string argName = old ? "oldredeem" : "newredeem";
            string redeemScript = ConfigReader.GetOrDefault<string>(argName, null);

            if (string.IsNullOrEmpty(redeemScript))
                throw new ArgumentException($"Please specify the {(old ? "old" : "new")} redeem script.");

            try
            {
                Script script = new Script(redeemScript);

                return script;
            }
            catch (Exception)
            {
                throw new ArgumentException($"Please specify a valid {(old ? "old" : "new")} redeem script.");
            }
        }

        private static void HandleSwitchGenerateFundsRecoveryTransaction(string[] args)
        {
            ConfigReader = new TextFileConfiguration(args);

            ConfirmArguments(ConfigReader, "network", "datadir", "oldredeem", "newredeem");

            Script newRedeemScript = GetRedeemScriptFromArguments(false);
            Script oldRedeemScript = GetRedeemScriptFromArguments(true);

            string dataDirPath = GetDataDirFromArguments();
            Key privateKey;

            try
            {
                var tool = new KeyTool(dataDirPath);
                privateKey = tool.LoadPrivateKey();
            }
            catch (Exception)
            {
                throw new ArgumentException($"Private key file not found in '{dataDirPath}'.");
            }

            // The old redeem script must contains my public key in order for me to contribute signatures.
            PayToMultiSigTemplateParameters oldParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(oldRedeemScript);
            if (!oldParams.PubKeys.Any(pubKey => pubKey == privateKey.PubKey))
                throw new ArgumentException("Only members of the old federation can provide signatures to recover the multisig funds.");

            // Verify the validity of the new script.
            PayToMultiSigTemplateParameters newParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(oldRedeemScript);

            if (newParams.SignatureCount > newParams.PubKeys.Length)
                throw new ArgumentException("The quorum of the new script has to be smaller than the number of members within the federation.");

            if (newParams.SignatureCount < newParams.PubKeys.Length / 2)
                throw new ArgumentException("The quorum of the new script has to be greater than half of the members within the federation.");

            (Network mainChain, Network sideChain) = GetMainAndSideChainNetworksFromArguments();

            BitcoinAddress oldSidechainMultisigAddress = oldRedeemScript.Hash.GetAddress(sideChain);
            Console.WriteLine("Old Sidechan P2SH: " + oldSidechainMultisigAddress.ScriptPubKey);
            Console.WriteLine("Old Sidechain Multisig address: " + oldSidechainMultisigAddress);

            BitcoinAddress newSidechainMultisigAddress = newRedeemScript.Hash.GetAddress(sideChain);
            Console.WriteLine("New Sidechan P2SH: " + newSidechainMultisigAddress.ScriptPubKey);
            Console.WriteLine("New Sidechain Multisig address: " + newSidechainMultisigAddress);

            Console.WriteLine($"Creating funds recovery transaction for {sideChain.Name}.");
            Transaction sideChainTx = (new RecoveryTransactionCreator()).CreateFundsRecoveryTransaction(sideChain, mainChain, dataDirPath, oldRedeemScript, newRedeemScript);
            Console.WriteLine("Sidechain Funds recovery transaction: " + sideChainTx.ToHex(sideChain));

            BitcoinAddress oldMainchainMultisigAddress = oldRedeemScript.Hash.GetAddress(mainChain);
            Console.WriteLine("Old Mainchain P2SH: " + oldMainchainMultisigAddress.ScriptPubKey);
            Console.WriteLine("Old Mainchain Multisig address: " + oldMainchainMultisigAddress);

            BitcoinAddress newMainchainMultisigAddress = newRedeemScript.Hash.GetAddress(mainChain);
            Console.WriteLine("New Mainchain P2SH: " + newMainchainMultisigAddress.ScriptPubKey);
            Console.WriteLine("New Mainchain Multisig address: " + newMainchainMultisigAddress);

            Console.WriteLine($"Creating funds recovery transaction for {mainChain.Name}.");
            Transaction mainChainTx = (new RecoveryTransactionCreator()).CreateFundsRecoveryTransaction(sideChain, mainChain, dataDirPath, oldRedeemScript, newRedeemScript);
            Console.WriteLine("Mainchain Funds recovery transaction: " + sideChainTx.ToHex(sideChain));
        }
    }
}
