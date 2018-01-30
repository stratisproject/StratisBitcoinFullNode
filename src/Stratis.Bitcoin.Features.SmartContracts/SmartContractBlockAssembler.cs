using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
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
        // The total amount that will be refunded to contract senders
        // Keep going from here
        private Money refundSender = 0;
        private List<TxOut> refundOutputs = new List<TxOut>();
        
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

        // Copied from PowBlockAssembler, got rid of junk 
        public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
        {
            this.pblock = this.pblocktemplate.Block; // Pointer for convenience.
            this.scriptPubKeyIn = scriptPubKeyIn;

            this.CreateCoinbase();
            this.ComputeBlockVersion();


            medianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.lockTimeCutoff = PowConsensusValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
                ? medianTimePast
                : this.pblock.Header.Time;

            this.fIncludeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

            // add transactions from the mempool
            int nPackagesSelected;
            int nDescendantsUpdated;
            this.AddTransactions(out nPackagesSelected, out nDescendantsUpdated);

            lastBlockTx = this.blockTx;
            lastBlockSize = this.blockSize;
            lastBlockWeight = this.blockWeight;

            // TODO: Implement Witness Code
            // pblocktemplate->CoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());
            this.pblocktemplate.VTxFees[0] = -this.fees;
            this.coinbase.Outputs[0].Value = this.fees + this.consensusLoop.Validator.GetProofOfWorkReward(this.height);
            this.pblocktemplate.TotalFee = this.fees;

            int nSerializeSize = this.pblock.GetSerializedSize();

            this.UpdateHeaders();
            this.TestBlockValidity();

            return this.pblocktemplate;
        }

        protected override void AddToBlock(TxMempoolEntry iter)
        {
            // always add transaction to block.
            base.AddToBlock(iter);

            // get contract transaction - there will only ever be 1. 
            TxOut contractTxOut = iter.Transaction.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsSmartContractExec);

            // if is boring transaction, return
            if (contractTxOut == null)
                return;

            SmartContractTransaction scTransaction = new SmartContractTransaction(contractTxOut, iter.Transaction);
            AttemptToAddContractCallToBlock(iter, scTransaction);
        }

        private void AttemptToAddContractCallToBlock(TxMempoolEntry iter, SmartContractTransaction scTransaction)
        {
            // what reasons would cause us to not add the transaction to the block?
            Money relayFee = iter.Fee - scTransaction.TotalGas;



            IContractStateRepository track = this.stateRoot.StartTracking();
            // TODO: Optimise so this conversion isn't happening every time.
            ulong height = Convert.ToUInt64(this.height);
            ulong difficulty = 0; // TODO: Fix obviously this.consensusLoop.Chain.GetWorkRequired(this.network, this.height);
            SmartContractTransactionExecutor exec = new SmartContractTransactionExecutor(track, this.decompiler, this.validator, this.gasInjector, scTransaction, height, difficulty);
            SmartContractExecutionResult result = exec.Execute();

            if (result.Revert)
            {
                // TODO: Expend gas here
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
