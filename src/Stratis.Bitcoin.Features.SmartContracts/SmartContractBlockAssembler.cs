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
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockAssembler : PowBlockAssembler
    {
        private List<TxOut> refundOutputs = new List<TxOut>();

        private ContractStateRepositoryRoot stateRoot;
        private ContractStateRepositoryRoot currentStateRepository;

        private readonly SmartContractDecompiler decompiler;
        private readonly ISmartContractGasInjector gasInjector;
        private readonly SmartContractValidator validator;

        private uint160 coinbaseAddress;
        private readonly CoinView coinView;
        private ulong difficulty;

        public SmartContractBlockAssembler(
            IConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolLock,
            ITxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ChainedBlock chainTip,
            ILoggerFactory loggerFactory,
            ContractStateRepositoryRoot stateRoot,
            SmartContractDecompiler decompiler,
            SmartContractValidator validator,
            ISmartContractGasInjector gasInjector,
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

        public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
        {
            this.difficulty = this.consensusLoop.Chain.GetWorkRequired(this.network, this.consensusLoop.Tip.Height);
            this.SetCoinbaseAddress(GetSenderUtil.GetAddressFromScript(scriptPubKeyIn));
            this.currentStateRepository = this.stateRoot.GetSnapshotTo(this.consensusLoop.Tip.Header.HashStateRoot.ToBytes());
            base.CreateNewBlock(scriptPubKeyIn, fMineWitnessTx);
            this.coinbase.Outputs.AddRange(this.refundOutputs);
            return this.pblocktemplate;
        }

        /// <summary>
        /// This will be removed once we have implemented the new assemblers.
        /// </summary>
        /// <param name="scriptPubKeyIn"></param>
        public void SetCoinbaseAddress(uint160 address)
        {
            this.coinbaseAddress = address;
        }

        /// <summary>
        /// The block header for smart contract blocks is identical to the standard block,
        /// except it also has a second 32-byte root, the state root. This byte array
        /// represents the current state of contract code, storage and balances, and can
        /// be used in conjunction with getSnapshotTo at any time to recreate this state.
        /// </summary>
        protected override void UpdateHeaders()
        {
            base.UpdateHeaders();

            this.pblock.Header.HashStateRoot = new uint256(this.currentStateRepository.Root);
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

            var smartContractCarrier = SmartContractCarrier.Deserialize(mempoolEntry.Transaction, smartContractTxOut);
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
            ISmartContractExecutionResult result = ExecuteContractFeesAndRefunds(carrier, mempoolEntry, (ulong)this.height, this.difficulty);

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

        public ISmartContractExecutionResult ExecuteContractFeesAndRefunds(SmartContractCarrier carrier, TxMempoolEntry txMempoolEntry, ulong height, ulong difficulty)
        {
            IContractStateRepository nestedStateRepository = this.currentStateRepository.StartTracking();

            var executor = new SmartContractTransactionExecutor(nestedStateRepository, this.decompiler, this.validator, this.gasInjector, carrier, height, difficulty, this.coinbaseAddress, this.network);
            ISmartContractExecutionResult executionResult = executor.Execute();

            // Update state--------------------------------
            if (executionResult.Revert)
                nestedStateRepository.Rollback();
            else
                nestedStateRepository.Commit();
            //---------------------------------------------

            var toRefund = CalculateRefund(carrier, executionResult);
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

            return executionResult;
        }

        /// <summary>
        /// Calculates the refund amount.
        /// <para>
        /// If an <see cref="OutOfGasException"/> was thrown no refund will be done.
        /// </para>
        /// </summary>
        private ulong CalculateRefund(SmartContractCarrier carrier, ISmartContractExecutionResult result)
        {
            if (result.Exception is OutOfGasException)
                return 0;

            ulong toRefund = carrier.GasCostBudget - (result.GasUnitsUsed * carrier.GasUnitPrice);
            return toRefund;
        }

        /// <summary>
        /// Create the script to send the relevant funds back to the user.
        /// TODO: Multiple refunds to same user should be consolidated to 1 TxOut to save space
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