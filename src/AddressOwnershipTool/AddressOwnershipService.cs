using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository;

namespace AddressOwnershipTool
{
    public class AddressOwnershipService
    {
        private const string distributedTransactionsFilename = "Distributed.csv";
        private const string ownershipFilename = "ownership.csv";
        private const decimal splitThreshold = 10_000m * 100_000_000m; // In stratoshi
        private const decimal splitCount = 10;

        private readonly Network network;
        private readonly string ownershipFilePath;
        private List<DistributedOwnershipTransaction> distributedTransactions;
        private List<OwnershipTransaction> ownershipTransactions;
        private int straxApiPort;
        
        public AddressOwnershipService(bool testnet, bool loadFiles = true)
        {
            this.network = testnet ? new StratisTest() : new StratisMain();

            this.straxApiPort = testnet ? 27103 : 17103;

            this.ownershipFilePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Only needed for -validate and -distribute
            if (loadFiles)
            {
                this.LoadAlreadyDistributedTransactions();
                this.LoadSwapTransactionFile();
            }
        }

        private void LoadAlreadyDistributedTransactions()
        {
            Console.WriteLine($"Loading already distributed transactions...");

            if (File.Exists(Path.Combine(this.ownershipFilePath, distributedTransactionsFilename)))
            {
                using (var reader = new StreamReader(Path.Combine(this.ownershipFilePath, distributedTransactionsFilename)))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false }))
                {
                    this.distributedTransactions = csv.GetRecords<DistributedOwnershipTransaction>().ToList();
                }
            }
            else
            {
                using (FileStream file = File.Create(Path.Combine(this.ownershipFilePath, distributedTransactionsFilename)))
                {
                    file.Close();
                }

                this.distributedTransactions = new List<DistributedOwnershipTransaction>();
            }
        }

        private void LoadSwapTransactionFile()
        {
            Console.WriteLine($"Loading transaction file...");

            // First check if the ownership file has been created.
            if (File.Exists(Path.Combine(this.ownershipFilePath, ownershipFilename)))
            {
                // If so populate the list from disk.
                using (var reader = new StreamReader(Path.Combine(this.ownershipFilePath, ownershipFilename)))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
                {
                    this.ownershipTransactions = csv.GetRecords<OwnershipTransaction>().ToList();
                }
            }
            else
            {
                Console.WriteLine("A transaction file has not been created yet, is this correct? (y/n)");
                int result = Console.Read();
                if (result != 121 && result != 89)
                {
                    Console.WriteLine("Exiting...");
                    return;
                }

                this.ownershipTransactions = new List<OwnershipTransaction>();
            }
        }

        public void Validate(string sigFileFolder)
        {
            var straxApiClient = new NodeApiClient($"http://localhost:{this.straxApiPort}/api");
            var stratisApiClient = new NodeApiClient($"http://localhost:{this.network.DefaultAPIPort}/api");

            decimal newRecordsFundsTotal = 0;
            foreach (string file in Directory.GetFiles(sigFileFolder))
            {
                if (!file.EndsWith(".csv"))
                    continue;

                Console.WriteLine($"Validating signature file '{file}'...");

                foreach (string line in File.ReadLines(file))
                {
                    try
                    {
                        // TODO: Modify this to use CsvHelper too?
                        string[] data = line.Split(";");

                        if (data.Length != 3)
                            continue;

                        if (data[0].Equals("Address"))
                            continue;

                        string address = data[0].Trim();
                        string destination = data[1].Trim();
                        string signature = data[2].Trim();

                        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(destination) || string.IsNullOrWhiteSpace(signature))
                        {
                            Console.WriteLine($"Malformed record: {line}");
                            continue;
                        }

                        if (this.ownershipTransactions.Any(s => s.SignedAddress == address))
                        {
                            //Console.WriteLine($"Ownership already proven for address: {address}. Ignoring this signature.");

                            continue;
                        }

                        // The address string is the actual message in this case.
                        var pubKey = PubKey.RecoverFromMessage(address, signature);

                        if (pubKey.Hash.ScriptPubKey.GetDestinationAddress(this.network).ToString() != address)
                        {
                            //Console.WriteLine($"Invalid signature for address '{address}'!");
                        }

                        if (!straxApiClient.ValidateAddress(destination))
                        {
                            //Console.WriteLine($"The provided Strax address was invalid: {destination}");

                            continue;
                        }

                        //Console.WriteLine($"Validated signature for address '{address}'");

                        decimal balance = stratisApiClient.GetAddressBalance(address);

                        if (balance <= 0)
                        {
                            //Console.WriteLine($"Address {address} has a zero balance, skipping it.");

                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Address {address} has a balance of {Money.Satoshis(balance).ToUnit(MoneyUnit.BTC)}.");
                        }

                        newRecordsFundsTotal += balance;

                        // We checked for an existing record already, so it is safe to add it now.
                        this.ownershipTransactions.Add(new OwnershipTransaction()
                        {
                            SenderAmount = balance,
                            StraxAddress = destination,
                            // We set this to the source address on the Stratis chain, to ensure only one record exists per unique address.
                            SignedAddress = address
                        });
                    }
                    catch
                    {
                        Console.WriteLine($"Error processing signature for '{line}'!");
                    }
                }

                using (var writer = new StreamWriter(Path.Combine(this.ownershipFilePath, ownershipFilename)))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(this.ownershipTransactions);
                }
            }

            Console.WriteLine($"There are {this.ownershipTransactions.Count} ownership transactions so far to process.");
            Console.WriteLine($"There are {Money.Satoshis(this.ownershipTransactions.Sum(s => s.SenderAmount)).ToUnit(MoneyUnit.BTC)} STRAT with ownership proved.");
            Console.WriteLine($"Of this, {Money.Satoshis(newRecordsFundsTotal).ToUnit(MoneyUnit.BTC)} STRAT came from new records.");
        }

        public void StratisXExport(string privKeyFile, string destinationAddress)
        {
            var lines = File.ReadLines(privKeyFile);

            foreach (var line in lines)
            {
                // Skip comments
                if (line.Trim().StartsWith("#"))
                    continue;

                // If it isn't at least long enough to contain the WIF then ignore the line
                if (line.Trim().Length < 53)
                    continue;

                try
                {
                    string[] data = line.Trim().Split(" ");

                    string privKey = data[0];

                    Key privateKey = Key.Parse(privKey, this.network);

                    string address = privateKey.PubKey.GetAddress(this.network).ToString();

                    string message = $"{address}";

                    string signature = privateKey.SignMessage(message);

                    this.OutputToFile(address, destinationAddress, signature);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error creating signature with private key file line '{line}'");
                }
            }
        }

        public void SbfnExport(string walletName, string walletPassword, string destinationAddress, bool deepExport = false)
        {
            var nodeSettings = new NodeSettings(this.network);

            // First check if sqlite wallet is being used.
            var walletRepository = new SQLiteWalletRepository(nodeSettings.LoggerFactory, nodeSettings.DataFolder, nodeSettings.Network, new DateTimeProvider(), new ScriptAddressReader());
            walletRepository.Initialize(false);

            Wallet wallet = null;

            try
            {
                wallet = walletRepository.GetWallet(walletName);
            }
            catch
            {
            }

            if (wallet != null)
            {
                this.HdAddressExport(wallet, walletPassword, destinationAddress, deepExport);

                return;
            }

            Console.WriteLine($"No SQL wallet with name {walletName} was found in folder {nodeSettings.DataDir}! Checking for legacy JSON wallet.");

            var fileStorage = new FileStorage<Wallet>(nodeSettings.DataFolder.WalletPath);

            if (fileStorage.Exists(walletName + ".wallet.json"))
            {
                wallet = fileStorage.LoadByFileName(walletName + ".wallet.json");
            }

            if (wallet != null)
            {
                this.HdAddressExport(wallet, walletPassword, destinationAddress, deepExport);

                return;
            }

            Console.WriteLine($"No legacy wallet with name {walletName} was found in folder {nodeSettings.DataFolder.WalletPath}!");
        }

        public void HdAddressExport(Wallet wallet, string walletPassword, string destinationAddress, bool deepExport = false)
        {
            foreach (HdAddress address in wallet.GetAllAddresses())
            {
                if (address.Transactions.Count == 0 && !deepExport)
                    continue;

                this.ExportAddress(wallet, address, walletPassword, destinationAddress);
            }
        }

        public void ExportAddress(Wallet wallet, HdAddress address, string walletPassword, string destinationAddress)
        {
            ISecret privateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address);

            string message = $"{address.Address}";

            string signature = privateKey.PrivateKey.SignMessage(message);

            OutputToFile(address.Address, destinationAddress, signature);
        }

        public void OutputToFile(string address, string destinationAddress, string signature)
        {
            string export = $"{address};{destinationAddress};{signature}";

            Console.WriteLine(export);

            using (StreamWriter sw = File.AppendText(destinationAddress + ".csv"))
            {
                sw.WriteLine(export);
            }
        }

        private List<RecipientModel> GetRecipients(string destinationAddress, decimal amount)
        {
            if (amount < splitThreshold)
            {
                return new List<RecipientModel> { new RecipientModel { DestinationAddress = destinationAddress, Amount = Money.Satoshis(amount).ToUnit(MoneyUnit.BTC).ToString() } };
            }

            var recipientList = new List<RecipientModel>();

            for (int i = 0; i < splitCount; i++)
            {
                recipientList.Add(new RecipientModel()
                {
                    DestinationAddress = destinationAddress,
                    Amount = Money.Satoshis(amount / splitCount).ToUnit(MoneyUnit.BTC).ToString()
                });
            }

            return recipientList;
        }

        public void BuildAndSendDistributionTransactions(string walletName, string walletPassword, string accountName, bool send = false)
        {
            var straxApiClient = new NodeApiClient($"http://localhost:{this.straxApiPort}/api");

            int count = 0;
            decimal total = 0;

            foreach (OwnershipTransaction ownershipTransaction in this.ownershipTransactions)
            {
                if (this.distributedTransactions.Any(d => d.SourceAddress == ownershipTransaction.SignedAddress))
                {
                    Console.WriteLine($"Already distributed: {ownershipTransaction.SignedAddress} -> {ownershipTransaction.StraxAddress}, {Money.Satoshis(ownershipTransaction.SenderAmount).ToUnit(MoneyUnit.BTC)} STRAT");

                    continue;
                }

                if (!send)
                {
                    count++;
                    total += ownershipTransaction.SenderAmount;

                    Console.WriteLine($"Simulate send of {Money.FromUnit(ownershipTransaction.SenderAmount, MoneyUnit.Satoshi)} to address {ownershipTransaction.StraxAddress}");

                    continue;
                }
                
                try
                {
                    var distributedSwapTransaction = new DistributedOwnershipTransaction(ownershipTransaction);

                    List<RecipientModel> recipients = GetRecipients(ownershipTransaction.StraxAddress, ownershipTransaction.SenderAmount);

                    WalletBuildTransactionModel builtTransaction = straxApiClient.BuildTransaction(walletName, walletPassword, accountName, recipients);

                    distributedSwapTransaction.TransactionBuilt = true;

                    straxApiClient.SendTransaction(builtTransaction.Hex);

                    distributedSwapTransaction.TransactionSent = true;
                    distributedSwapTransaction.TransactionSentHash = builtTransaction.TransactionId.ToString();

                    if (send)
                        Console.WriteLine($"Swap transaction built and sent to {distributedSwapTransaction.StraxAddress}: {Money.Satoshis(distributedSwapTransaction.SenderAmount).ToUnit(MoneyUnit.BTC)}");

                    // Append to the file.
                    using (FileStream stream = File.Open(Path.Combine(this.ownershipFilePath, distributedTransactionsFilename), FileMode.Append))
                    using (var writer = new StreamWriter(stream))
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecord(distributedSwapTransaction);
                        csv.NextRecord();
                    }

                    this.distributedTransactions.Add(distributedSwapTransaction);

                    // Give some time for the transaction to begin relaying.
                    Thread.Sleep(1000);

                    count++;
                    total += ownershipTransaction.SenderAmount;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    break;
                }
            }

            Console.WriteLine($"Count: {count} distribution transactions");
            Console.WriteLine($"Total: {Money.FromUnit(total, MoneyUnit.Satoshi)} STRAT");
        }
    }
}
