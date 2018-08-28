﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Defines how a proof of work block will be built on a proof of stake network.
    /// </summary>
    public sealed class SmartContractPosPowBlockDefinition : BlockDefinition
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly IStakeChain stakeChain;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private readonly IStakeValidator stakeValidator;

        private uint160 coinbaseAddress;
        private readonly ICoinView coinView;
        private readonly ISmartContractExecutorFactory executorFactory;
        private readonly List<TxOut> refundOutputs = new List<TxOut>();
        private readonly List<Receipt> receipts = new List<Receipt>();
        private readonly ContractStateRepositoryRoot stateRoot;
        private ContractStateRepositoryRoot stateSnapshot;

        public SmartContractPosPowBlockDefinition(
            ICoinView coinView,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            ISmartContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network,
            IStakeChain stakeChain,
            IStakeValidator stakeValidator,
            ContractStateRepositoryRoot stateRoot)
            : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network)
        {
            this.coinView = coinView;
            this.executorFactory = executorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.stakeChain = stakeChain;
            this.stakeValidator = stakeValidator;
            this.stateRoot = stateRoot;
        }

        /// <inheritdoc/>
        public override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            this.logger.LogTrace("()");

            TxOut smartContractTxOut = mempoolEntry.Transaction.TryGetSmartContractTxOut();
            if (smartContractTxOut == null)
            {
                this.logger.LogTrace("Transaction does not contain smart contract information.");

                base.AddTransactionToBlock(mempoolEntry.Transaction);
                base.UpdateBlockStatistics(mempoolEntry);
                base.UpdateTotalFees(mempoolEntry.Fee);
            }
            else
            {
                this.logger.LogTrace("Transaction contains smart contract information.");

                // We HAVE to first execute the smart contract contained in the transaction
                // to ensure its validity before we can add it to the block.
                ISmartContractExecutionResult result = this.ExecuteSmartContract(mempoolEntry);
                this.AddTransactionToBlock(mempoolEntry.Transaction);
                this.UpdateBlockStatistics(mempoolEntry);
                this.UpdateTotalFees(result.Fee);

                // If there are refunds, add them to the block.
                if (result.Refunds.Any())
                {
                    this.refundOutputs.AddRange(result.Refunds);
                    this.logger.LogTrace("{0} refunds were added.", result.Refunds.Count);
                }

                // Add internal transactions made during execution.
                if (result.InternalTransaction != null)
                {
                    this.AddTransactionToBlock(result.InternalTransaction);
                    this.logger.LogTrace("Internal {0}:{1} was added.", nameof(result.InternalTransaction), result.InternalTransaction.GetHash());
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.logger.LogTrace("()");

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetAddressFromScript(scriptPubKey);
            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-createnewblock", getSenderResult.Error));

            this.coinbaseAddress = getSenderResult.Sender;

            this.stateSnapshot = this.stateRoot.GetSnapshotTo(((SmartContractBlockHeader)this.ConsensusManager.Tip.Header).HashStateRoot.ToBytes());

            this.refundOutputs.Clear();
            this.receipts.Clear();

            base.OnBuild(chainTip, scriptPubKey);

            this.coinbase.Outputs.AddRange(this.refundOutputs);

            this.logger.LogTrace("(-)");

            return this.BlockTemplate;
        }

        /// <inheritdoc/>
        public override void UpdateHeaders()
        {
            this.logger.LogTrace("()");

            base.UpdateBaseHeaders();

            this.block.Header.Bits = this.stakeValidator.GetNextTargetRequired(this.stakeChain, this.ChainTip, this.Network.Consensus, false);

            var scHeader = (SmartContractBlockHeader)this.block.Header;

            scHeader.HashStateRoot = new uint256(this.stateSnapshot.Root);

            UpdateReceiptRoot(scHeader);

            UpdateLogsBloom(scHeader);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sets the receipt root based on all the receipts generated in smart contract execution inside this block.
        /// </summary>
        private void UpdateReceiptRoot(SmartContractBlockHeader scHeader)
        {
            List<uint256> leaves = this.receipts.Select(x => x.GetHash()).ToList();
            bool mutated = false; // TODO: Do we need this?
            scHeader.ReceiptRoot = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }

        /// <summary>
        /// Sets the bloom filter for all logs that occurred in this block's execution.
        /// </summary>
        private void UpdateLogsBloom(SmartContractBlockHeader scHeader)
        {
            Bloom logsBloom = new Bloom();
            foreach (Receipt receipt in this.receipts)
            {
                logsBloom.Or(receipt.Bloom);
            }
            scHeader.LogsBloom = logsBloom;
        }

        /// <summary>
        /// Execute the contract and add all relevant fees and refunds to the block.
        /// </summary>
        /// <remarks>TODO: At some point we need to change height to a ulong.</remarks> 
        private ISmartContractExecutionResult ExecuteSmartContract(TxMempoolEntry mempoolEntry)
        {
            this.logger.LogTrace("()");

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetSender(mempoolEntry.Transaction, this.coinView, this.inBlock.Select(x => x.Transaction).ToList());
            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-addcontracttoblock", getSenderResult.Error));

            ISmartContractTransactionContext transactionContext = new SmartContractTransactionContext((ulong)this.height, this.coinbaseAddress, mempoolEntry.Fee, getSenderResult.Sender, mempoolEntry.Transaction);
            ISmartContractExecutor executor = this.executorFactory.CreateExecutor(this.stateSnapshot, transactionContext);
            ISmartContractExecutionResult result = executor.Execute(transactionContext);

            var receipt = new Receipt(
                new uint256(this.stateSnapshot.Root),
                result.GasConsumed,
                result.Logs.ToArray()
            );
            this.receipts.Add(receipt);

            this.logger.LogTrace("(-)");

            return result;
        }
    }
}