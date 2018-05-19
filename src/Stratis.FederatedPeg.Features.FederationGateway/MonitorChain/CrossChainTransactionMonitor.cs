using System.Text;
using Microsoft.Extensions.Logging;

using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using DBreeze.Utils;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class CrossChainTransactionInfo
    {
        /// <summary>
        /// The hash of the source transaction that originates the fund transfer. 
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
        public uint256 CrossChainTransactionId { get; set; } = uint256.Zero;

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
        // Logging.
        private readonly ILogger logger;

        // Our session manager.
        private IPartialTransactionSessionManager partialTransactionSessionManager;

        // The redeem Script we are monitoring.
        private Script script;

        // The gateway settings from the config file.
        private readonly FederationGatewaySettings federationGatewaySettings;

        // The network we are running on. Will be a Stratis chain for mainchain or a sidechain.
        private readonly Network network;

        private ConcurrentChain concurrentChain;

        private IInitialBlockDownloadState initialBlockDownloadState;

        private ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public CrossChainTransactionMonitor(ILoggerFactory loggerFactory, 
            Network network,
            ConcurrentChain concurrentChain,
            FederationGatewaySettings federationGatewaySettings,
            IPartialTransactionSessionManager partialTransactionSessionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.partialTransactionSessionManager = partialTransactionSessionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.concurrentChain = concurrentChain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
        }

        /// <summary>
        /// Saves the store during shutdown.
        /// </summary>
        public void Dispose()
        {
            this.crossChainTransactionAuditor?.Dispose();
        }
        
        /// <inheritdoc/>>
        public void Initialize()
        {
            // Read the relevant multisig address with help of the folder manager.
            var memberFolderManager = new MemberFolderManager(federationGatewaySettings.FederationFolder);

            // Initialize chain specifics.
            var chain = network.ToChain();
            var multiSigAddress = memberFolderManager.ReadAddress(chain);
            this.script = BitcoinAddress.Create(multiSigAddress, this.network).ScriptPubKey;

            // Load the auditor if present.
            this.crossChainTransactionAuditor?.Initialize();
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
                    var stringResult = OpReturnDataReader.GetStringFromOpReturn(this.logger, network, transaction, out var opReturnDataType);
                    if (opReturnDataType == OpReturnDataType.Unknown) continue;

                    if (opReturnDataType == OpReturnDataType.Address)
                    {
                        this.ProcessAddress(transaction.GetHash(), stringResult, txOut.Value, blockNumber, block.GetHash());
                        continue;
                    }

                    if (opReturnDataType == OpReturnDataType.Hash)
                    {
                        this.crossChainTransactionAuditor?.AddCounterChainTransactionId(transaction.GetHash(), uint256.Parse(stringResult));
                        this.crossChainTransactionAuditor?.Commit();
                        this.logger.LogInformation($"AddCounterChainTransactionId: {stringResult} for transaction {transaction.GetHash()}.");
                        continue;
                    }
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
            var chainBlockTip = this.concurrentChain.GetBlock(block.GetHash());
            int blockNumber = chainBlockTip.Height;

            // If we are in IBD we do nothing.
            // TODO: This may not be required as the BlockObserver seems to only trigger for new blocks.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation($"{this.federationGatewaySettings.MemberName}  MonitorChain ({this.network.ToChain()}) in IBD: blockNumber {blockNumber} not processed.");
                return;
            }

            this.logger.LogDebug(
                $"{this.federationGatewaySettings.MemberName} Monitor Processing Block: {blockNumber} on {this.network.ToChain()}");

            foreach (var transaction in block.Transactions)
                this.ProcessTransaction(transaction, block, blockNumber);
        }

        private void ProcessAddress(uint256 transactionHash, string destinationAddress, Money amount, int blockNumber, uint256 blockHash)
        {
            // This looks like a deposit or withdrawal transaction. Record the info.
            var crossChainTransactionInfo = new CrossChainTransactionInfo
            {
                DestinationAddress = destinationAddress,
                Amount = amount,
                BlockNumber = blockNumber,
                BlockHash = blockHash,
                TransactionHash = transactionHash
            };

            this.crossChainTransactionAuditor?.AddCrossChainTransactionInfo(crossChainTransactionInfo);

            // Commit audit as we know we have a new record. 
            this.crossChainTransactionAuditor?.Commit();

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