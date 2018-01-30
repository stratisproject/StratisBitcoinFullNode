using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockAssembler : PowBlockAssembler
    {
        private readonly IContractStateRepository stateRoot;
        private readonly SmartContractDecompiler decompiler;
        private readonly SmartContractValidator validator;
        private readonly SmartContractGasInjector gasInjector;

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
            AssemblerOptions options = null) : base(consensusLoop, network, mempoolLock, mempool, dateTimeProvider, chainTip, loggerFactory, options)
        {
            this.stateRoot = stateRoot;
            this.decompiler = decompiler;
            this.validator = validator;
            this.gasInjector = gasInjector;
        }

        protected override void AddToBlock(TxMempoolEntry iter)
        {
            // always add transaction to block.
            base.AddToBlock(iter);

            // if is boring transaction, return
            if (!iter.Transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractExec))
                return;

            foreach (TxOut txOut in iter.Transaction.Outputs)
            {
                if (txOut.ScriptPubKey.IsSmartContractExec)
                {
                    var scTransaction = new SmartContractTransaction(txOut, iter.Transaction);
                    AttemptToAddContractCallToBlock(iter, scTransaction);
                }
            }
        }

        private void AttemptToAddContractCallToBlock(TxMempoolEntry iter, SmartContractTransaction scTransaction)
        {
            // what reasons would cause us to not add the transaction to the block?

            IContractStateRepository track = this.stateRoot.StartTracking();
            // TODO: Optimise so this conversion isn't happening every time.
            ulong height = Convert.ToUInt64(this.height);
            ulong difficulty = 0; // TODO: Fix obviously
            //ulong difficulty = this.consensusLoop.Chain.GetWorkRequired(this.network, this.height);
            SmartContractTransactionExecutor exec = new SmartContractTransactionExecutor(track, this.decompiler, this.validator, this.gasInjector, scTransaction, height, difficulty);
            SmartContractExecutionResult result = exec.Execute();

            if (result.Revert)
            {
                track.Rollback();
                return;
            }

            foreach(Transaction transaction in result.Transactions)
            {
                this.pblock.AddTransaction(transaction);
                // Not sure about these other things
                this.pblocktemplate.VTxFees.Add(0);
                this.pblocktemplate.TxSigOpsCost.Add(0); 
                // add to blocksize
                // add to blockweight
                // add to blockTx
                // add to block sigopscost
                // add to this.fees
            }

            // Add to coinbase
            // Refund transaction
        }
    }
}
