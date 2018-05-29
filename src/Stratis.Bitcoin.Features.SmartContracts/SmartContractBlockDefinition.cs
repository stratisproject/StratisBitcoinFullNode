using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
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

        /// <inheritdoc/>
        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKeyIn)
        {
            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetAddressFromScript(this.Network, scriptPubKeyIn);

            if (!getSenderResult.Success)
            {
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-createnewblock", getSenderResult.Error));
            }

            this.coinbaseAddress = getSenderResult.Sender;

            this.stateSnapshot = this.stateRoot.GetSnapshotTo(((SmartContractBlockHeader)this.ConsensusLoop.Tip.Header).HashStateRoot.ToBytes());

            this.refundOutputs.Clear();

            base.OnBuild(chainTip, scriptPubKeyIn);

            this.coinbase.Outputs.AddRange(this.refundOutputs);

            return this.BlockTemplate;
        }

        /// <summary>
        /// The block header for smart contract blocks is identical to the standard block,
        /// except it also has a second 32-byte root, the state root. This byte array
        /// represents the current state of contract code, storage and balances, and can
        /// be used in conjunction with getSnapshotTo at any time to recreate this state.
        /// </summary>
        public override void OnUpdateHeaders()
        {
            this.logger.LogTrace("()");

            this.block.Header.HashPrevBlock = this.ChainTip.HashBlock;
            this.block.Header.UpdateTime(this.DateTimeProvider.GetTimeOffset(), this.Network, this.ChainTip);
            this.block.Header.Bits = this.block.Header.GetWorkRequired(this.Network, this.ChainTip);
            this.block.Header.Nonce = 0;
            ((SmartContractBlockHeader)this.block.Header).HashStateRoot = new uint256(this.stateSnapshot.Root);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Overrides the <see cref="AddToBlock(TxMempoolEntry)"/> behaviour of <see cref="BlockDefinitionProofOfWork"/>.
        /// <para>
        /// Determine whether or not the mempool entry contains smart contract execution 
        /// code. If not, then add to the block as per normal. Else extract and deserialize 
        /// the smart contract code from the TxOut's ScriptPubKey.
        /// </para>
        /// </summary>
        protected override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            TxOut smartContractTxOut = mempoolEntry.TryGetSmartContractTxOut();
            if (smartContractTxOut == null)
                base.AddToBlock(mempoolEntry);
            else
                this.AddContractToBlock(mempoolEntry);
        }

        /// <summary>
        /// Execute the contract and add all relevant fees and refunds to the block.
        /// </summary>
        /// <remarks>TODO: At some point we need to change height to a ulong.</remarks> 
        private void AddContractToBlock(TxMempoolEntry mempoolEntry)
        {
            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetSender(this.Network, mempoolEntry.Transaction, this.coinView, this.inBlock.Select(x => x.Transaction).ToList());

            if (!getSenderResult.Success)
            {
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-addcontracttoblock", getSenderResult.Error));
            }

            ISmartContractExecutor executor = this.executorFactory.CreateExecutor(
                (ulong) this.height,
                this.coinbaseAddress,
                mempoolEntry.Fee,
                getSenderResult.Sender,
                this.stateSnapshot,
                mempoolEntry.Transaction);

            ISmartContractExecutionResult result = executor.Execute();

            // Add fee from the execution result to the block.
            this.BlockTemplate.VTxFees.Add(result.Fee);
            this.fees += result.Fee;

            // If there are refunds, add them to the block
            if (result.Refunds.Any())
                this.refundOutputs.AddRange(result.Refunds);

            // Add the mempool entry transaction to the block 
            // and adjust BlockSize, BlockWeight and SigOpsCost.
            this.block.AddTransaction(mempoolEntry.Transaction);
            this.BlockTemplate.TxSigOpsCost.Add(mempoolEntry.SigOpCost);

            if (this.NeedSizeAccounting)
                this.BlockSize += mempoolEntry.Transaction.GetSerializedSize();

            this.BlockWeight += mempoolEntry.TxWeight;
            this.BlockTx++;
            this.BlockSigOpsCost += mempoolEntry.SigOpCost;
            this.inBlock.Add(mempoolEntry);

            // Add internal transactions made during execution
            if (result.InternalTransaction != null)
            {
                this.block.AddTransaction(result.InternalTransaction);
                if (this.NeedSizeAccounting)
                    this.BlockSize += result.InternalTransaction.GetSerializedSize();
                this.BlockTx++;
            }
        }

        /// <inheritdoc/>
        public override void OnTestBlockValidity()
        {
        }
    }
}