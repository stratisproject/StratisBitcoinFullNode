using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway.CounterChain;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.MonitorChain;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    ///<inheritdoc/>
    internal class CrossChainTransactionMonitor : ICrossChainTransactionMonitor
    {
        private readonly ILogger logger;

        // Our session manager.
        private readonly IMonitorChainSessionManager monitorChainSessionManager;

        private readonly ICounterChainSessionManager counterChainSessionManager;

        private readonly Script monitoredMultisigScript;

        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly Network network;

        private readonly ConcurrentChain concurrentChain;

        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly ICrossChainTransactionAuditor crossChainTransactionAuditor;

        // The minimum transfer amount permissible.
        // (Prevents spamming of network.)
        private readonly Money minimumTransferAmount;

        private readonly IOpReturnDataReader opReturnDataReader;

        public CrossChainTransactionMonitor(
            ILoggerFactory loggerFactory,
            Network network,
            ConcurrentChain concurrentChain,
            IFederationGatewaySettings federationGatewaySettings,
            IMonitorChainSessionManager monitorChainSessionManager,
            ICounterChainSessionManager counterChainSessionManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            IOpReturnDataReader opReturnDataReader,
            ICrossChainTransactionAuditor crossChainTransactionAuditor = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(monitorChainSessionManager, nameof(monitorChainSessionManager));
            Guard.NotNull(counterChainSessionManager, nameof(counterChainSessionManager));
            Guard.NotNull(federationGatewaySettings, nameof(federationGatewaySettings));
            Guard.NotNull(concurrentChain, nameof(concurrentChain));
            Guard.NotNull(initialBlockDownloadState, nameof(initialBlockDownloadState));
            Guard.NotNull(opReturnDataReader, nameof(opReturnDataReader));
            Guard.NotNull(crossChainTransactionAuditor, nameof(crossChainTransactionAuditor));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.monitorChainSessionManager = monitorChainSessionManager;
            this.counterChainSessionManager = counterChainSessionManager;
            this.federationGatewaySettings = federationGatewaySettings;
            this.concurrentChain = concurrentChain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.opReturnDataReader = opReturnDataReader;
            this.crossChainTransactionAuditor = crossChainTransactionAuditor;
            this.monitoredMultisigScript = this.federationGatewaySettings.MultiSigAddress.ScriptPubKey;
            this.minimumTransferAmount = new Money(1.0m, MoneyUnit.BTC);
        }

        /// <inheritdoc />
        /// <summary>
        /// Saves the store during shutdown.
        /// </summary>
        public void Dispose()
        {
            this.crossChainTransactionAuditor.Dispose();
        }

        /// <inheritdoc/>>
        public void Initialize(IFederationGatewaySettings federationGatewaySettings)
        {
            // Load the auditor if present.
            this.crossChainTransactionAuditor.Initialize();
        }

        /// <inheritdoc/>>
        public void ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));
            this.logger.LogTrace("({0}:'{1}')", nameof(block.GetHash), block.GetHash());

            ChainedHeader newTip = this.concurrentChain.GetBlock(block.GetHash());
            if (newTip == null)
            {
                this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                return;
            }

            var chainBlockTip = this.concurrentChain.GetBlock(block.GetHash());
            int blockNumber = chainBlockTip.Height;

            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogTrace("MonitorChain ({0}) in IBD: blockNumber {1} not processed.", this.network.ToChain(), blockNumber);
                return;
            }

            this.logger.LogTrace("Monitor Processing Block: {0} on {1}", blockNumber, this.network.ToChain());

            // Create a session to process the transaction.
            // Tell our Session Manager that we can start a new session.
            MonitorChainSession monitorSession = new MonitorChainSession(blockNumber, this.federationGatewaySettings.FederationPublicKeys.Select(f => f.ToHex()).ToArray(), this.federationGatewaySettings.PublicKey);

            foreach (var transaction in block.Transactions)
            {
                foreach (var txOut in transaction.Outputs)
                {
                    if (txOut.ScriptPubKey != this.monitoredMultisigScript) continue;
                    var stringResult = this.opReturnDataReader.GetStringFromOpReturn(transaction, out var opReturnDataType);

                    switch (opReturnDataType)
                    {
                        case OpReturnDataType.Unknown:
                            this.logger.LogTrace("Received transaction with unknown OP_RETURN data: {0}. Transaction hash: {1}.", stringResult, transaction.GetHash());
                            continue;
                        case OpReturnDataType.Address:
                            this.logger.LogInformation("Processing received transaction with address: {0}. Transaction hash: {1}.", stringResult, transaction.GetHash());
                            CrossChainTransactionInfo trxInfo = this.ProcessAddress(transaction.GetHash(), stringResult, txOut.Value, blockNumber, block.GetHash());

                            if (trxInfo != null)
                            {
                                this.crossChainTransactionAuditor.AddCrossChainTransactionInfo(trxInfo);

                                // Commit audit as we know we have a new record. 
                                this.crossChainTransactionAuditor.Commit();

                                monitorSession.CrossChainTransactions.Add(trxInfo);
                            }
                            continue;
                        case OpReturnDataType.BlockHeight:
                            var blockHeight = int.Parse(stringResult);
                            this.logger.LogInformation("AddCounterChainTransactionId: {0} for session in block {1}.", transaction.GetHash(), blockHeight);
                            this.counterChainSessionManager.AddCounterChainTransactionId(blockHeight, transaction.GetHash());
                            continue;
                        case OpReturnDataType.Hash:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            if (!monitorSession.CrossChainTransactions.Any()) return;

            this.logger.LogInformation("AddCounterChainTransactionId: Found {0} transactions to process in block with height {1}.", monitorSession.CrossChainTransactions.Count, monitorSession.BlockNumber);
            this.monitorChainSessionManager.RegisterMonitorSession(monitorSession);
            this.monitorChainSessionManager.CreateSessionOnCounterChain(this.federationGatewaySettings.SourceChainApiPort, monitorSession);

        }

        private CrossChainTransactionInfo ProcessAddress(uint256 transactionHash, string destinationAddress, Money amount, int blockNumber, uint256 blockHash)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}')", nameof(transactionHash), transactionHash, nameof(amount), amount, nameof(destinationAddress), destinationAddress, nameof(blockNumber), blockNumber, nameof(blockHash), blockHash);

            if (amount < this.minimumTransferAmount)
            {
                this.logger.LogInformation($"The transaction {transactionHash} has less than the MinimumTransferAmount.  Ignoring. ");
                return null;
            }

            var crossChainTransactionInfo = new CrossChainTransactionInfo
            {
                DestinationAddress = destinationAddress,
                Amount = amount,
                BlockNumber = blockNumber,
                BlockHash = blockHash,
                TransactionHash = transactionHash
            };

            this.logger.LogInformation("Crosschain Transaction Found on : {0}", this.network.ToChain());
            this.logger.LogInformation("CrosschainTransactionInfo: {0}", crossChainTransactionInfo);
            return crossChainTransactionInfo;
        }
    }
}