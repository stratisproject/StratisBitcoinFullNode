using System.Text;
using Microsoft.Extensions.Logging;

using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class CrossChainTransactionInfo
    {
        /// <summary>
        /// The hash of the source (op_return) transaction that originates the fund transfer. 
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionHash { get; set; }

        /// <summary>
        /// The amount of the requested fund transfer.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// The final destination of funds (on the counter chain).
        /// </summary>
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The block number where the source transaction resides.
        /// </summary>
        public int BlockNumber { get; set; }

        /// <summary>
        /// The hash of the block where the transaction resides.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// The hash of the destination transaction that moved the funds into the counterchain destination.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 CrossChainTransactionId { get; set; } = null;

        /// <summary>
        /// Helper to generate a json respresentation of this structure for logging/debugging.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// The CrossChainTransactionMonitor examines transactions to detect deposit and withdrawal transactions.
    /// The transactions we are interested move funds into out multi-sig and also have an additional output with
    /// an OP_RETURN and the destination address on the counter chain.
    /// When an appropriate transaction is detected we create a session in our session manager and continue monitoring
    /// our chain. 
    /// </summary>
    internal class CrossChainTransactionMonitor : ICrossChainTransactionMonitor
    {
        // The storeFileName varies depending on the chain we are monitoring (mainchain=deposits or sidechain=withdrawals).
        private string storeFileName;

        // Logging.
        private readonly ILogger logger;

        // The storage used to store the cross chain transactions.
        private readonly FileStorage<CrossChainTransactionStore> fileStorage;

        // The in memory cross chain transaction store.
        private CrossChainTransactionStore crossChainTransactionStore;

        // Tracks the block number for info.
        private int blockNumber = 0;

        // Our session manager.
        private IPartialTransactionSessionManager partialTransactionSessionManager;

        // The redeem Script we are monitoring.
        private Script script;

        // The gateway settings from the config file.
        private readonly FederationGatewaySettings federationGatewaySettings;

        // The network we are running on. Will be a Stratis chain for mainchain or a sidechain.
        private readonly Network network;

        public CrossChainTransactionMonitor(ILoggerFactory loggerFactory, Network network, DataFolder dataFolder,
            FederationGatewaySettings federationGatewaySettings,
            IPartialTransactionSessionManager partialTransactionSessionManager)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.fileStorage = new FileStorage<CrossChainTransactionStore>(dataFolder.WalletPath);
            this.network = network;
            this.partialTransactionSessionManager = partialTransactionSessionManager;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        /// <summary>
        /// Saves the store during shutdown.
        /// </summary>
        public void Dispose()
        {
            this.SaveCrossChainTransactionStore();
        }
        
        /// <inheritdoc/>>
        public void Initialize()
        {
            // Read the relevant multisig address with help of the folder manager.
            var memberFolderManager = new MemberFolderManager(federationGatewaySettings.FederationFolder);

            // Initialize chain specifics.
            var chain = network.ToChain();
            this.storeFileName = chain == Chain.Mainchain
                ? "deposit_transaction_store.json"
                : "withdrawal_transaction_store.json";
            var multiSigAddress = memberFolderManager.ReadAddress(chain);
            this.script = BitcoinAddress.Create(multiSigAddress, this.network).ScriptPubKey;

            // Load the store.
            this.crossChainTransactionStore = this.LoadCrossChainTransactionStore();
        }

        /// <inheritdoc/>>
        public void ProcessTransaction(Transaction transaction, Block block, int blockNumber)
        {
            // Look at each output in the transaction.
            foreach (var txOut in transaction.Outputs)
            {
                // Does the ScriptPubKey match the script that we are interested in?
                if (txOut.ScriptPubKey == this.script)
                {
                    // Ok we found the script in this transaction. Does it also have an OP_RETURN?
                    var destinationAddress = GetDestinationFromOpReturn(transaction);
                    if (destinationAddress == null) continue;

                    // This looks like a deposit or withdrawal transaction. Record the info.
                    var crossChainTransactionInfo = new CrossChainTransactionInfo
                    {
                        DestinationAddress = destinationAddress,
                        Amount = txOut.Value,
                        BlockNumber = blockNumber,
                        BlockHash = block.GetHash(),
                        TransactionHash = transaction.GetHash()
                    };
                    this.crossChainTransactionStore.Add(crossChainTransactionInfo);

                    // Save the store as we know we have a new record. 
                    this.SaveCrossChainTransactionStore();

                    // Create a session to process the transaction.
                    this.CreateSession(crossChainTransactionInfo);
        
                    // Log Info for info/diagnostics.
                    this.logger.LogInformation("()");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} Crosschain Transaction Found on : {this.network.ToChain()}");
                    this.logger.LogInformation(
                        $"{this.federationGatewaySettings.MemberName} CrosschainTransactionInfo: {crossChainTransactionInfo}");
                    this.logger.LogInformation("(-)");
                }
            }
        }

        /// <inheritdoc/>>
        public void CreateSession(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            // Tell our Session Manager that we can start a new session.
            this.partialTransactionSessionManager.CreateBuildAndBroadcastSession(crossChainTransactionInfo);
        }

        /// <inheritdoc/>>
        public void ProcessBlock(Block block)
        {
            //todo: use chained block?
            this.blockNumber += 1;

            this.logger.LogDebug(
                $"{this.federationGatewaySettings.MemberName} Monitor Processing Block: {blockNumber} on {this.network.ToChain()}");

            foreach (var transaction in block.Transactions)
                this.ProcessTransaction(transaction, block, blockNumber);
        }

        // Save the store.
        private void SaveCrossChainTransactionStore()
        {
            if (this.crossChainTransactionStore != null) //if initialize was not called
                this.fileStorage.SaveToFile(this.crossChainTransactionStore, storeFileName);
        }

        // Load the store (creates if no store yet).
        private CrossChainTransactionStore LoadCrossChainTransactionStore()
        {
            if (this.fileStorage.Exists(storeFileName))
                return this.fileStorage.LoadByFileName(storeFileName);

            // Create a new empty store.
            var transactionStore = new CrossChainTransactionStore();
            this.fileStorage.SaveToFile(transactionStore, storeFileName);
            return transactionStore;
        }

        // Examines the outputs of the transaction to see if an OP_RETURN is present.
        // Assumes a positive result yields an address: todo: is this safe?
        private static string GetDestinationFromOpReturn(Transaction transaction)
        {
            string destination = null;
            foreach (var txOut in transaction.Outputs)
            {
                var data = txOut.ScriptPubKey.ToBytes();
                if ((OpcodeType) data[0] == OpcodeType.OP_RETURN)
                    destination = Encoding.UTF8.GetString(data).Remove(0, 2);
            }
            return destination;
        }
    }
}