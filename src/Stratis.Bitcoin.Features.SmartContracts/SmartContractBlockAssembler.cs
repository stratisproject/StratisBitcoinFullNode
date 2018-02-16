using System;
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
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.ContractValidation;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockAssembler : PowBlockAssembler
    {
        private Money refundSender = 0;
        private List<TxOut> refundOutputs = new List<TxOut>();

        private readonly IContractStateRepository stateRoot;
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
            AssemblerOptions options = null) : base(consensusLoop, network, mempoolLock, mempool, dateTimeProvider, chainTip, loggerFactory, options)
        {
            this.stateRoot = stateRoot;
            this.decompiler = decompiler;
            this.validator = validator;
            this.gasInjector = gasInjector;
            this.coinView = coinView;
        }

        // Copied from PowBlockAssembler, got rid of comments 
        public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
        {
            this.pblock = this.pblocktemplate.Block; // Pointer for convenience.
            this.scriptPubKeyIn = scriptPubKeyIn;

            this.coinbaseAddress = new uint160(this.scriptPubKeyIn.GetDestinationPublicKeys().FirstOrDefault().Hash.ToBytes(), false); // TODO: This ugly af

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

            this.pblocktemplate.VTxFees[0] = -this.fees;
            this.coinbase.Outputs[0].Value = this.fees + this.consensusLoop.Validator.GetProofOfWorkReward(this.height);
            this.coinbase.Outputs.AddRange(this.refundOutputs);
            this.pblocktemplate.TotalFee = this.fees;

            int nSerializeSize = this.pblock.GetSerializedSize();

            this.UpdateHeaders();
            this.TestBlockValidity();

            this.stateRoot.Commit();

            return this.pblocktemplate;
        }

        protected override void UpdateHeaders()
        {
            base.UpdateHeaders();
            this.pblock.Header.HashStateRoot = new uint256(this.stateRoot.GetRoot());
        }

        protected override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            //Determine whether or not this mempool entry contains smart contract execution code.
            TxOut smartContractTxOut = mempoolEntry.TryGetSmartContractTxOut();
            if (smartContractTxOut == null)
            {
                //If no smart contract exists then add to block as per normal.
                base.AddToBlock(mempoolEntry);
            }
            else
            {
                //Else extract and deserialize the smart contract code from the TxOut's ScriptPubKey.
                SmartContractCarrier smartContractCarrier = SmartContractCarrier.Deserialize(mempoolEntry.Transaction, smartContractTxOut);
                smartContractCarrier.Sender = GetSenderUtil.GetSender(mempoolEntry.Transaction, this.coinView, this.inBlock.Select(x => x.Transaction).ToList());

                AddContractCallToBlock(mempoolEntry, smartContractCarrier);
            }
        }

        private void AddContractCallToBlock(TxMempoolEntry mempoolEntry, SmartContractCarrier smartContractCarrier)
        {
            IContractStateRepository track = this.stateRoot.StartTracking();
            ulong height = Convert.ToUInt64(this.height);// TODO: Optimise so this conversion isn't happening every time.
            ulong difficulty = 0; // TODO: Fix obviously this.consensusLoop.Chain.GetWorkRequired(this.network, this.height);

            var executor = new SmartContractTransactionExecutor(track, this.decompiler, this.validator, this.gasInjector, smartContractCarrier, height, difficulty, this.coinbaseAddress);
            ulong gasToSpend = smartContractCarrier.TotalGas;
            SmartContractExecutionResult result = executor.Execute();

            //Update state
            if (result.Revert)
                track.Rollback();
            else
                track.Commit();

            ulong toRefund = gasToSpend - result.GasUsed * smartContractCarrier.GasPrice;
            ulong txFeeAndGas = mempoolEntry.Fee - toRefund;

            // Add original transaction and fees to block
            this.pblock.AddTransaction(mempoolEntry.Transaction);
            this.pblocktemplate.VTxFees.Add(txFeeAndGas);
            this.pblocktemplate.TxSigOpsCost.Add(mempoolEntry.SigOpCost);
            if (this.needSizeAccounting)
                this.blockSize += mempoolEntry.Transaction.GetSerializedSize();

            this.blockWeight += mempoolEntry.TxWeight;
            this.blockTx++;
            this.blockSigOpsCost += mempoolEntry.SigOpCost;
            this.fees += txFeeAndGas;
            this.inBlock.Add(mempoolEntry);

            // Add internal transactions made during execution
            foreach (Transaction transaction in result.InternalTransactions)
            {
                this.pblock.AddTransaction(transaction);
                if (this.needSizeAccounting)
                    this.blockSize += transaction.GetSerializedSize();
                this.blockTx++;
            }

            // Setup refunds
            this.refundSender += toRefund;
            Script senderScript = new Script(
                OpcodeType.OP_DUP,
                OpcodeType.OP_HASH160,
                Op.GetPushOp(smartContractCarrier.Sender.ToBytes()),
                OpcodeType.OP_EQUALVERIFY,
                OpcodeType.OP_CHECKSIG
            );
            this.refundOutputs.Add(new TxOut(toRefund, senderScript));
        }
    }
}
