using System;
using Microsoft.Extensions.Logging;

using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.MonitorChain;

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

    ///<inheritdoc/>
    internal class CrossChainTransactionMonitor : ICrossChainTransactionMonitor
    {
        // Logging.
        private readonly ILogger logger;

        // Our session manager.
        private readonly IMonitorChainSessionManager monitorChainSessionManager;

        // The redeem Script we are monitoring.
        private Script script;

        // The gateway settings from the config file.
        private readonly FederationGatewaySettings federationGatewaySettings;

        // The network we are running on. Will be a Stratis chain for mainchain or a sidechain.
        private readonly Network network;

        private readonly ConcurrentChain concurrentChain;

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ICrossChainTransactionAuditor crossChainTransactionAuditor;

        public CrossChainTransactionMonitor(ILoggerFactory loggerFactory, 
            Network network,
            ConcurrentChain concurrentChain,
            FederationGatewaySettings federationGatewaySettings,
            IMonitorChainSessionManager monitorChainSessionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.monitorChainSessionManager = monitorChainSessionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.concurrentChain = concurrentChain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
        }

        /// <inheritdoc />
        /// <summary>
        /// Saves the store during shutdown.
        /// </summary>
        public void Dispose()
        {
            this.crossChainTransactionAuditor?.Dispose();
        }
        
        /// <inheritdoc/>>
        public void Initialize(FederationGatewaySettings federationGatewaySettings)
        {
            // Read the relevant multisig address with help of the folder manager.
            this.script = this.federationGatewaySettings.MultiSigAddress.ScriptPubKey;

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
                if (txOut.ScriptPubKey != this.script) continue;
                // Ok we found the script in this transaction. Does it also have an OP_RETURN?
                var stringResult = OpReturnDataReader.GetStringFromOpReturn(this.logger, network, transaction, out var opReturnDataType);
                switch (opReturnDataType)
                {
                    case OpReturnDataType.Unknown:
                        continue;
                    case OpReturnDataType.Address:
                        this.ProcessAddress(transaction.GetHash(), stringResult, txOut.Value, blockNumber, block.GetHash());
                        continue;
                    case OpReturnDataType.Hash:
                        this.crossChainTransactionAuditor?.AddCounterChainTransactionId(transaction.GetHash(), uint256.Parse(stringResult));
                        this.crossChainTransactionAuditor?.Commit();
                        this.logger.LogInformation($"AddCounterChainTransactionId: {stringResult} for transaction {transaction.GetHash()}.");
                        continue;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <inheritdoc/>>
        public void CreateSession(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            // Tell our Session Manager that we can start a new session.
            this.monitorChainSessionManager.CreateMonitorSession(crossChainTransactionInfo);
        }

        /// <inheritdoc/>>
        public void ProcessBlock(Block block)
        {
            var chainBlockTip = this.concurrentChain.GetBlock(block.GetHash());
            int blockNumber = chainBlockTip.Height;

            // If we are in IBD we do nothing.
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogDebug($"{this.federationGatewaySettings.MemberName}  MonitorChain ({this.network.ToChain()}) in IBD: blockNumber {blockNumber} not processed.");
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