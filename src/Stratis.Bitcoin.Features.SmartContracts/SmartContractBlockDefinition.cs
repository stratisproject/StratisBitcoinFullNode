﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractBlockDefinition : BlockDefinition
    {
        private uint160 coinbaseAddress;
        private readonly CoinView coinView;
        private readonly ISmartContractExecutorFactory executorFactory;
        private readonly ILogger logger;
        private readonly List<TxOut> refundOutputs = new List<TxOut>();
        private readonly ContractStateRepositoryRoot stateRoot;
        private ContractStateRepositoryRoot stateSnapshot;

        public SmartContractBlockDefinition(
            CoinView coinView,
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ISmartContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            ContractStateRepositoryRoot stateRoot)
            : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network)
        {
            this.coinView = coinView;
            this.executorFactory = executorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.stateRoot = stateRoot;
        }

        /// <summary>
        /// Overrides the <see cref="AddToBlock(TxMempoolEntry)"/> behaviour of <see cref="BlockDefinitionProofOfWork"/>.
        /// <para>
        /// Determine whether or not the mempool entry contains smart contract execution 
        /// code. If not, then add to the block as per normal. Else extract and deserialize 
        /// the smart contract code from the TxOut's ScriptPubKey.
        /// </para>
        /// </summary>
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
        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKeyIn)
        {
            this.logger.LogTrace("()");

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetAddressFromScript(scriptPubKeyIn);
            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-createnewblock", getSenderResult.Error));

            this.coinbaseAddress = getSenderResult.Sender;

            this.stateSnapshot = this.stateRoot.GetSnapshotTo(((SmartContractBlockHeader)this.ConsensusLoop.Tip.Header).HashStateRoot.ToBytes());

            this.refundOutputs.Clear();

            base.OnBuild(chainTip, scriptPubKeyIn);

            this.coinbase.Outputs.AddRange(this.refundOutputs);

            this.logger.LogTrace("(-)");

            return this.BlockTemplate;
        }

        /// <summary>
        /// The block header for smart contract blocks is identical to the standard block,
        /// except it also has a second 32-byte root, the state root. This byte array
        /// represents the current state of contract code, storage and balances, and can
        /// be used in conjunction with getSnapshotTo at any time to recreate this state.
        /// </summary>
        public override void UpdateHeaders()
        {
            this.logger.LogTrace("()");

            this.UpdateBaseHeaders();

            this.block.Header.Bits = this.block.Header.GetWorkRequired(this.Network, this.ChainTip);
            ((SmartContractBlockHeader)this.block.Header).HashStateRoot = new uint256(this.stateSnapshot.Root);

            this.logger.LogTrace("(-)");
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

            this.logger.LogTrace("(-)");

            return result;
        }
    }
}