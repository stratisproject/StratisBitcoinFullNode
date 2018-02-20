using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.Exceptions;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockAssembler : PowBlockAssembler
    {
        private List<TxOut> refundOutputs = new List<TxOut>();

        private IContractStateRepository stateRoot;
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractValidator validator;
        private readonly SmartContractGasInjector gasInjector;
        private readonly CoinView coinView;
        private uint160 coinbaseAddress;

        public SmartContractBlockAssembler(
            IConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolLock,
            ITxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ChainedBlock chainTip,
            ILoggerFactory loggerFactory,
            IContractStateRepository stateRoot,
            SmartContractDecompiler decompiler,
            SmartContractValidator validator,
            SmartContractGasInjector gasInjector,
            CoinView coinView,
            AssemblerOptions options = null)
            : base(consensusLoop, network, mempoolLock, mempool, dateTimeProvider, chainTip, loggerFactory, options)
        {
            this.stateRoot = stateRoot;
            this.decompiler = decompiler;
            this.validator = validator;
            this.gasInjector = gasInjector;
            this.coinView = coinView;
        }
        /// <summary>
        /// TODO: Add documentation on the block headers for smart contract blocks differ.
        /// </summary>
        protected override void UpdateHeaders()
        {
            base.UpdateHeaders();

            this.pblock.Header.HashStateRoot = new uint256(this.stateRoot.GetRoot());
        }

        /// <summary>
        /// Overrides the <see cref="AddToBlock(TxMempoolEntry)"/> behaviour of <see cref="PowBlockAssembler"/>.
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
            {
                base.AddToBlock(mempoolEntry);
                return;
            }

            SmartContractCarrier smartContractCarrier = SmartContractCarrier.Deserialize(mempoolEntry.Transaction, smartContractTxOut);
            smartContractCarrier.Sender = GetSenderUtil.GetSender(mempoolEntry.Transaction, this.coinView, this.inBlock.Select(x => x.Transaction).ToList());

            this.AddContractCallToBlock(mempoolEntry, smartContractCarrier);
        }

        /// <summary>
        /// Executes the contract and adds all relevant fees and refunds to the block.
        /// </summary>
        /// <remarks>
        /// TODO: At some point we need to change height to a ulong.
        /// </remarks> 
        private void AddContractCallToBlock(TxMempoolEntry mempoolEntry, SmartContractCarrier carrier)
        {
            ulong difficulty = this.consensusLoop.Chain.GetWorkRequired(this.network, this.consensusLoop.Tip.Height);

            SmartContractExecutionResult result = ExecuteContractFeesAndRefunds(carrier, mempoolEntry, (ulong)this.height, difficulty);

            // Add the mempool entry transaction to the block 
            // and adjust BlockSize, BlockWeight and SigOpsCost
            this.pblock.AddTransaction(mempoolEntry.Transaction);
            this.pblocktemplate.TxSigOpsCost.Add(mempoolEntry.SigOpCost);

            if (this.needSizeAccounting)
                this.blockSize += mempoolEntry.Transaction.GetSerializedSize();

            this.blockWeight += mempoolEntry.TxWeight;
            this.blockTx++;
            this.blockSigOpsCost += mempoolEntry.SigOpCost;
            this.inBlock.Add(mempoolEntry);
            //---------------------------------------------

            // Add internal transactions made during execution
            foreach (Transaction transaction in result.InternalTransactions)
            {
                this.pblock.AddTransaction(transaction);
                if (this.needSizeAccounting)
                    this.blockSize += transaction.GetSerializedSize();
                this.blockTx++;
            }
            //---------------------------------------------
        }

        public SmartContractExecutionResult ExecuteContractFeesAndRefunds(SmartContractCarrier carrier, TxMempoolEntry txMempoolEntry, ulong height, ulong difficulty)
        {
            IContractStateRepository track = this.stateRoot.StartTracking();

            var executor = new SmartContractTransactionExecutor(track, this.decompiler, this.validator, this.gasInjector, carrier, height, difficulty, this.coinbaseAddress);
            SmartContractExecutionResult result = executor.Execute();

            // Update state--------------------------------
            if (result.Revert)
                track.Rollback();
            else
                track.Commit();
            //---------------------------------------------

            var toRefund = CalculateRefund(carrier, result);
            if (toRefund > 0)
            {
                ulong txFeeAndGas = txMempoolEntry.Fee - toRefund;
                this.pblocktemplate.VTxFees.Add(txFeeAndGas);
                this.fees += txFeeAndGas;

                ProcessRefund(carrier, toRefund);
            }
            else
            {
                this.pblocktemplate.VTxFees.Add(txMempoolEntry.Fee);
                this.fees += txMempoolEntry.Fee;
            }

            return result;
        }

        /// <summary>
        /// Calculates the refund amount.
        /// <para>
        /// If an <see cref="OutOfGasException"/> was thrown no refund will be done.
        /// </para>
        /// </summary>
        private ulong CalculateRefund(SmartContractCarrier carrier, SmartContractExecutionResult result)
        {
            if (result.Exception is OutOfGasException)
                return 0;

            ulong toRefund = carrier.GasCostBudget - (result.GasUnitsUsed * carrier.GasUnitPrice);
            return toRefund;
        }

        /// <summary>
        /// Create the script to send the relevant funds back to the user.
        /// </summary>
        private void ProcessRefund(SmartContractCarrier carrier, ulong toRefund)
        {
            var senderScript = new Script(
                OpcodeType.OP_DUP,
                OpcodeType.OP_HASH160,
                Op.GetPushOp(carrier.Sender.ToBytes()),
                OpcodeType.OP_EQUALVERIFY,
                OpcodeType.OP_CHECKSIG
            );

            this.refundOutputs.Add(new TxOut(toRefund, senderScript));
        }
    }
}