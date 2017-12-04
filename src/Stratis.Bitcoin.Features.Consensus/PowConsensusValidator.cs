﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides functionality for verifying validity of PoW block.
    /// </summary>
    /// <remarks>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</remarks>
    public class PowConsensusValidator
    {
        /// <summary>Flags that determine how transaction should be validated in non-consensus code.</summary>
        public static Transaction.LockTimeFlags StandardLocktimeVerifyFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of block header hash checkpoints.</summary>
        protected readonly ICheckpoints Checkpoints;

        /// <summary>Consensus parameters.</summary>
        public NBitcoin.Consensus ConsensusParams { get; }

        /// <summary>Consensus options.</summary>
        public PowConsensusOptions ConsensusOptions { get; }

        /// <summary>Keeps track of how much time different actions took to execute and how many times they were executed.</summary>
        public ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowConsensusValidator"/> class.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="checkpoints">Provider of block header hash checkpoints.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public PowConsensusValidator(Network network, ICheckpoints checkpoints, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(network.Consensus.Option<PowConsensusOptions>(), nameof(network.Consensus.Options));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.ConsensusParams = network.Consensus;
            this.ConsensusOptions = network.Consensus.Option<PowConsensusOptions>();
            this.dateTimeProvider = dateTimeProvider;
            this.PerformanceCounter = new ConsensusPerformanceCounter(this.dateTimeProvider);
            this.Checkpoints = checkpoints;
        }

        /// <summary>
        /// Checks if <paramref name="context.BlockValidationContext.Block"/> has a valid PoW header.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.HighHash">Thrown if block doesn't have a valid PoW header.</exception>
        public virtual void CheckBlockHeader(ContextInformation context)
        {
            if (context.CheckPow && !context.BlockValidationContext.Block.Header.CheckProofOfWork())
                ConsensusErrors.HighHash.Throw();

            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetWorkRequired(context.Consensus);
        }

        /// <summary>
        /// Context-dependent validity checks.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if one or more transactions are not finalized.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseHeight">Thrown if coinbase doesn't start with serialized block height.</exception>
        public virtual void ContextualCheckBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            DeploymentFlags deploymentFlags = context.Flags;

            int height = context.BestBlock == null ? 0 : context.BestBlock.Height + 1;

            // Start enforcing BIP113 (Median Time Past) using versionbits logic.
            DateTimeOffset lockTimeCutoff = deploymentFlags.LockTimeFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast) ?
                context.BestBlock.MedianTimePast :
                block.Header.BlockTime;

            // Check that all transactions are finalized.
            foreach (Transaction transaction in block.Transactions)
            {
                if (!transaction.IsFinal(lockTimeCutoff, height))
                {
                    this.logger.LogTrace("(-)[TX_NON_FINAL]");
                    ConsensusErrors.BadTransactionNonFinal.Throw();
                }
            }

            // Enforce rule that the coinbase starts with serialized block height.
            if (deploymentFlags.EnforceBIP34)
            {
                var expect = new Script(Op.GetPushOp(height));
                Script actual = block.Transactions[0].Inputs[0].ScriptSig;
                if (!this.StartWith(actual.ToBytes(true), expect.ToBytes(true)))
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_HEIGHT]");
                    ConsensusErrors.BadCoinbaseHeight.Throw();
                }
            }

            // Validation for witness commitments.
            // * We compute the witness hash (which is the hash including witnesses) of all the block's transactions, except the
            //   coinbase (where 0x0000....0000 is used instead).
            // * The coinbase scriptWitness is a stack of a single 32-byte vector, containing a witness nonce (unconstrained).
            // * We build a merkle tree with all those witness hashes as leaves (similar to the hashMerkleRoot in the block header).
            // * There must be at least one output whose scriptPubKey is a single 36-byte push, the first 4 bytes of which are
            //   {0xaa, 0x21, 0xa9, 0xed}, and the following 32 bytes are SHA256^2(witness root, witness nonce). In case there are
            //   multiple, the last one is used.
            bool haveWitness = false;
            if (deploymentFlags.ScriptFlags.HasFlag(ScriptVerify.Witness))
            {
                int commitpos = this.GetWitnessCommitmentIndex(block);
                if (commitpos != -1)
                {
                    uint256 hashWitness = this.BlockWitnessMerkleRoot(block, out bool unused);

                    // The malleation check is ignored; as the transaction tree itself
                    // already does not permit it, it is impossible to trigger in the
                    // witness tree.
                    WitScript witness = block.Transactions[0].Inputs[0].WitScript;
                    if ((witness.PushCount != 1) || (witness.Pushes.First().Length != 32))
                    {
                        this.logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        ConsensusErrors.BadWitnessNonceSize.Throw();
                    }

                    var hashed = new byte[64];
                    Buffer.BlockCopy(hashWitness.ToBytes(), 0, hashed, 0, 32);
                    Buffer.BlockCopy(witness.Pushes.First(), 0, hashed, 32, 32);
                    hashWitness = Hashes.Hash256(hashed);

                    if (!this.EqualsArray(hashWitness.ToBytes(), block.Transactions[0].Outputs[commitpos].ScriptPubKey.ToBytes(true).Skip(6).ToArray(), 32))
                    {
                        this.logger.LogTrace("(-)[WITNESS_MERKLE_MISMATCH]");
                        ConsensusErrors.BadWitnessMerkleMatch.Throw();
                    }

                    haveWitness = true;
                }
            }

            if (!haveWitness)
            {
                for (int i = 0; i < block.Transactions.Count; i++)
                {
                    if (block.Transactions[i].HasWitness)
                    {
                        this.logger.LogTrace("(-)[UNEXPECTED_WITNESS]");
                        ConsensusErrors.UnexpectedWitness.Throw();
                    }
                }
            }

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (this.GetBlockWeight(block) > this.ConsensusOptions.MaxBlockWeight)
            {
                this.logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Validates the UTXO set is correctly spent.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="taskScheduler">Task scheduler for creating tasks that would check validity of each transaction input.</param>
        /// <exception cref="ConsensusErrors.BadTransactionBIP30">Thrown if block contain transactions which 'overwrite' older transactions.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction tries to spend inputs that are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if transaction's height or time is lower then provided by SequenceLock for this block.</exception>
        /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if signature operation cost is greater then maximum block signature operation cost.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionScriptError">Thrown if not all inputs are valid (no double spends, scripts & sigs, amounts).</exception>
        public virtual void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.PerformanceCounter.AddProcessedBlocks(1);
            taskScheduler = taskScheduler ?? TaskScheduler.Default;

            if (!context.BlockValidationContext.SkipValidation)
            {
                if (flags.EnforceBIP30)
                {
                    foreach (Transaction tx in block.Transactions)
                    {
                        UnspentOutputs coins = view.AccessCoins(tx.GetHash());
                        if ((coins != null) && !coins.IsPrunable)
                        {
                            this.logger.LogTrace("(-)[BAD_TX_BIP_30]");
                            ConsensusErrors.BadTransactionBIP30.Throw();
                        }
                    }
                }
            }
            else this.logger.LogTrace("BIP30 validation skipped for checkpointed block at height {0}.", index.Height);

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();
            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                this.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.BlockValidationContext.SkipValidation)
                {
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        int[] prevheights;

                        if (!view.HaveInputs(tx))
                        {
                            this.logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                            ConsensusErrors.BadTransactionMissingInput.Throw();
                        }

                        prevheights = new int[tx.Inputs.Count];
                        // Check that transaction is BIP68 final.
                        // BIP68 lock checks (as opposed to nLockTime checks) must
                        // be in ConnectBlock because they require the UTXO set.
                        for (int j = 0; j < tx.Inputs.Count; j++)
                        {
                            prevheights[j] = (int)view.AccessCoins(tx.Inputs[j].PrevOut.Hash).Height;
                        }

                        if (!tx.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
                        {
                            this.logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                            ConsensusErrors.BadTransactionNonFinal.Throw();
                        }
                    }

                    // GetTransactionSignatureOperationCost counts 3 types of sigops:
                    // * legacy (always),
                    // * p2sh (when P2SH enabled in flags and excludes coinbase),
                    // * witness (when witness enabled in flags and excludes coinbase).
                    sigOpsCost += this.GetTransactionSignatureOperationCost(tx, view, flags);
                    if (sigOpsCost > this.ConsensusOptions.MaxBlockSigopsCost)
                        ConsensusErrors.BadBlockSigOps.Throw();

                    // TODO: Simplify this condition.
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        this.CheckInputs(tx, view, index.Height);
                        fees += view.GetValueIn(tx) - tx.TotalOut;
                        var txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            this.PerformanceCounter.AddProcessedInputs(1);
                            TxIn input = tx.Inputs[inputIndex];
                            int inputIndexCopy = inputIndex;
                            TxOut txout = view.GetOutputFor(input);
                            var checkInput = new Task<bool>(() =>
                            {
                                var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                var ctx = new ScriptEvaluationContext();
                                ctx.ScriptVerify = flags.ScriptFlags;
                                return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                            });
                            checkInput.Start(taskScheduler);
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinView(context, tx);
            }

            if (!context.BlockValidationContext.SkipValidation)
            {
                this.CheckBlockReward(context, fees, index.Height, block);

                bool passed = checkInputs.All(c => c.GetAwaiter().GetResult());
                if (!passed)
                {
                    this.logger.LogTrace("(-)[BAD_TX_SCRIPT]");
                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else this.logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Updates context's UTXO set.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="transaction">Transaction which outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.</param>
        protected virtual void UpdateCoinView(ContextInformation context, Transaction transaction)
        {
            this.logger.LogTrace("()");

            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            UnspentOutputSet view = context.Set;

            view.Update(transaction, index.Height);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Verifies that block has correct coinbase transaction with appropriate reward and fees summ.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="fees">Total amount of fees from transactions that are included in that block.</param>
        /// <param name="height">Block's height.</param>
        /// <param name="block">Block for which reward amount is checked.</param>
        /// <exception cref="ConsensusErrors.BadCoinbaseAmount">Thrown if coinbase transaction output value is larger than expected.</exception>
        protected virtual void CheckBlockReward(ContextInformation context, Money fees, int height, Block block)
        {
            this.logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks the maturity of UTXOs.
        /// </summary>
        /// <param name="coins">UTXOs to check the maturity of.</param>
        /// <param name="spendHeight">Height at which coins are attempted to be spent.</param>
        /// <exception cref="ConsensusErrors.BadTransactionPrematureCoinbaseSpending">Thrown if transaction tries to spend coins that are not mature.</exception>
        protected virtual void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            // If prev is coinbase, check that it's matured
            if (coins.IsCoinbase)
            {
                if ((spendHeight - coins.Height) < this.ConsensusOptions.CoinbaseMaturity)
                {
                    this.logger.LogTrace("Coinbase transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.ConsensusOptions.CoinbaseMaturity);
                    this.logger.LogTrace("(-)[COINBASE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinbaseSpending.Throw();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks that transaction's inputs are valid.
        /// </summary>
        /// <param name="transaction">Transaction to check.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="spendHeight">Height at which we are spending coins.</param>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction's inputs are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInputValueOutOfRange">Thrown if input value is out of range.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInBelowOut">Thrown if transaction inputs are less then outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeFee">Thrown if fees sum is negative.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionFeeOutOfRange">Thrown if fees value is out of range.</exception>
        public virtual void CheckInputs(Transaction transaction, UnspentOutputSet inputs, int spendHeight)
        {
            this.logger.LogTrace("({0}:{1})", nameof(spendHeight), spendHeight);

            if (!inputs.HaveInputs(transaction))
                ConsensusErrors.BadTransactionMissingInput.Throw();

            Money valueIn = Money.Zero;
            Money fees = Money.Zero;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                OutPoint prevout = transaction.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);

                this.CheckMaturity(coins, spendHeight);

                // Check for negative or overflow input values.
                valueIn += coins.TryGetOutput(prevout.N).Value;
                if (!this.MoneyRange(coins.TryGetOutput(prevout.N).Value) || !this.MoneyRange(valueIn))
                {
                    this.logger.LogTrace("(-)[BAD_TX_INPUT_VALUE]");
                    ConsensusErrors.BadTransactionInputValueOutOfRange.Throw();
                }
            }

            if (valueIn < transaction.TotalOut)
            {
                this.logger.LogTrace("(-)[TX_IN_BELOW_OUT]");
                ConsensusErrors.BadTransactionInBelowOut.Throw();
            }

            // Tally transaction fees.
            Money txFee = valueIn - transaction.TotalOut;
            if (txFee < 0)
            {
                this.logger.LogTrace("(-)[NEGATIVE_FEE]");
                ConsensusErrors.BadTransactionNegativeFee.Throw();
            }

            fees += txFee;
            if (!this.MoneyRange(fees))
            {
                this.logger.LogTrace("(-)[BAD_FEE]");
                ConsensusErrors.BadTransactionFeeOutOfRange.Throw();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Gets the proof of work reward amount for the block at provided height.
        /// </summary>
        /// <param name="height">Height of the block that we're calculating the reward for.</param>
        /// <returns>Reward amount.</returns>
        public virtual Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.ConsensusParams.SubsidyHalvingInterval;
            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.ConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;
            return subsidy;
        }

        /// <summary>
        /// Calculates total signature operation cost of a transaction.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for all transaction's inputs.</returns>
        public long GetTransactionSignatureOperationCost(Transaction transaction, UnspentOutputSet inputs, DeploymentFlags flags)
        {
            long signatureOperationCost = this.GetLegacySignatureOperationsCount(transaction) * this.ConsensusOptions.WitnessScaleFactor;

            if (transaction.IsCoinBase)
                return signatureOperationCost;

            if (flags.ScriptFlags.HasFlag(ScriptVerify.P2SH))
            {
                signatureOperationCost += this.GetP2SHSignatureOperationsCount(transaction, inputs) * this.ConsensusOptions.WitnessScaleFactor;
            }

            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                signatureOperationCost += this.CountWitnessSignatureOperation(transaction.Inputs[i].ScriptSig, prevout.ScriptPubKey, transaction.Inputs[i].WitScript, flags);
            }

            return signatureOperationCost;
        }

        /// <summary>
        /// Calculates signature operation cost for single transaction input.
        /// </summary>
        /// <param name="scriptSig">Signature script.</param>
        /// <param name="scriptPubKey">Script public key.</param>
        /// <param name="witness">Witness script.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for single transaction input.</returns>
        private long CountWitnessSignatureOperation(Script scriptSig, Script scriptPubKey, WitScript witness, DeploymentFlags flags)
        {
            witness = witness ?? WitScript.Empty;
            if (!flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                return 0;

            WitProgramParameters witParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(scriptPubKey);

            if (witParams?.Version == 0)
            {
                if (witParams.Program.Length == 20)
                    return 1;

                if (witParams.Program.Length == 32 && witness.PushCount > 0)
                {
                    Script subscript = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));
                    return subscript.GetSigOpCount(true);
                }
            }

            return 0;
        }

        /// <summary>
        /// Calculates pay-to-script-hash (BIP16) transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private uint GetP2SHSignatureOperationsCount(Transaction transaction, UnspentOutputSet inputs)
        {
            if (transaction.IsCoinBase)
                return 0;

            uint sigOps = 0;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                if (prevout.ScriptPubKey.IsPayToScriptHash)
                    sigOps += prevout.ScriptPubKey.GetSigOpCount(transaction.Inputs[i].ScriptSig);
            }

            return sigOps;
        }

        /// <summary>
        /// Checks block's validity.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.BadMerkleRoot">Thrown block's merkle root is corrupted.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicate">Thrown if block contains duplicated transaction that don't affect merkle root.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">Thrown if block exceeds maximum allowed size or doesn't contain any transaction.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseMissing">Thrown if block's first transaction is not coinbase.</exception>
        /// <exception cref="ConsensusErrors.BadMultipleCoinbase">Thrown if block contains more then one coinbase transactions.</exception>
        /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if block's signature operation cost is greater than maximum allowed one.</exception>
        public virtual void CheckBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;

            bool mutated;
            uint256 hashMerkleRoot2 = this.BlockMerkleRoot(block, out mutated);
            if (context.CheckMerkleRoot && (block.Header.HashMerkleRoot != hashMerkleRoot2))
            {
                this.logger.LogTrace("(-)[BAD_MERKLE_ROOT]");
                ConsensusErrors.BadMerkleRoot.Throw();
            }

            // Check for merkle tree malleability (CVE-2012-2459): repeating sequences
            // of transactions in a block without affecting the merkle root of a block,
            // while still invalidating it.
            if (mutated)
            {
                this.logger.LogTrace("(-)[BAD_TX_DUP]");
                ConsensusErrors.BadTransactionDuplicate.Throw();
            }

            // All potential-corruption validation must be done before we do any
            // transaction validation, as otherwise we may mark the header as invalid
            // because we receive the wrong transactions for it.
            // Note that witness malleability is checked in ContextualCheckBlock, so no
            // checks that use witness data may be performed here.

            // Size limits. Retain Stratis legacy static flags.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > this.ConsensusOptions.MaxBlockBaseSize) || 
                (this.GetSize(block, block.Header.NetworkOptions & NetworkOptions.POS) > this.ConsensusOptions.MaxBlockBaseSize))
            {
                this.logger.LogTrace("(-)[BAD_BLOCK_LEN]");
                ConsensusErrors.BadBlockLength.Throw();
            }

            // First transaction must be coinbase, the rest must not be
            if ((block.Transactions.Count == 0) || !block.Transactions[0].IsCoinBase)
            {
                this.logger.LogTrace("(-)[NO_COINBASE]");
                ConsensusErrors.BadCoinbaseMissing.Throw();
            }

            for (int i = 1; i < block.Transactions.Count; i++)
            {
                if (block.Transactions[i].IsCoinBase)
                {
                    this.logger.LogTrace("(-)[MULTIPLE_COINBASE]");
                    ConsensusErrors.BadMultipleCoinbase.Throw();
                }
            }

            // Check transactions
            foreach (Transaction tx in block.Transactions)
                this.CheckTransaction(tx);

            long sigOps = 0;
            foreach (Transaction tx in block.Transactions)
                sigOps += this.GetLegacySignatureOperationsCount(tx);

            if ((sigOps * this.ConsensusOptions.WitnessScaleFactor) > this.ConsensusOptions.MaxBlockSigopsCost)
            {
                this.logger.LogTrace("(-)[BAD_BLOCK_SIGOPS]");
                ConsensusErrors.BadBlockSigOps.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Calculates legacy transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <returns>Legacy signature operation cost for transaction.</returns>
        private long GetLegacySignatureOperationsCount(Transaction transaction)
        {
            long sigOps = 0;
            foreach (TxIn txin in transaction.Inputs)
                sigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in transaction.Outputs)
                sigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return sigOps;
        }

        /// <summary>
        /// Checks if transaction is valid.
        /// </summary>
        /// <param name="transaction">Transaction.</param>
        /// <exception cref="ConsensusErrors.BadTransactionNoInput">Thrown if transaction has no inputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNoOutput">Thrown if transaction has no outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionOversize">Thrown if transaction size is greater than maximum allowed size of a block.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeOutput">Thrown if at least one transaction output has negative value.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeOutput">Thrown if at least one transaction output value is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeTotalOutput">Thrown if sum of all transaction outputs is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicateInputs">Thrown if any of transaction inputs are duplicate.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseSize">Thrown if coinbase transaction is too small or too big.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNullPrevout">Thrown if transaction contains a null prevout.</exception>
        public virtual void CheckTransaction(Transaction transaction)
        {
            this.logger.LogTrace("()");

            // Basic checks that don't depend on any context.
            if (transaction.Inputs.Count == 0)
            {
                this.logger.LogTrace("(-)[TX_NO_INPUT]");
                ConsensusErrors.BadTransactionNoInput.Throw();
            }

            if (transaction.Outputs.Count == 0)
            {
                this.logger.LogTrace("(-)[TX_NO_OUTPUT]");
                ConsensusErrors.BadTransactionNoOutput.Throw();
            }

            // Size limits (this doesn't take the witness into account, as that hasn't been checked for malleability).
            if (this.GetSize(transaction, transaction.NetworkOptions & NetworkOptions.POS) > this.ConsensusOptions.MaxBlockBaseSize)
            {
                this.logger.LogTrace("(-)[TX_OVERSIZE]");
                ConsensusErrors.BadTransactionOversize.Throw();
            }

            // Check for negative or overflow output values
            long valueOut = 0;
            foreach (TxOut txout in transaction.Outputs)
            {
                if (txout.Value.Satoshi < 0)
                {
                    this.logger.LogTrace("(-)[TX_OUTPUT_NEGATIVE]");
                    ConsensusErrors.BadTransactionNegativeOutput.Throw();
                }

                if (txout.Value.Satoshi > this.ConsensusOptions.MaxMoney)
                {
                    this.logger.LogTrace("(-)[TX_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeOutput.Throw();
                }

                valueOut += txout.Value;
                if (!this.MoneyRange(valueOut))
                {
                    this.logger.LogTrace("(-)[TX_TOTAL_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeTotalOutput.Throw();
                }
            }

            // Check for duplicate inputs.
            var inOutPoints = new HashSet<OutPoint>();
            foreach (TxIn txin in transaction.Inputs)
            {
                if (inOutPoints.Contains(txin.PrevOut))
                {
                    this.logger.LogTrace("(-)[TX_DUP_INPUTS]");
                    ConsensusErrors.BadTransactionDuplicateInputs.Throw();
                }

                inOutPoints.Add(txin.PrevOut);
            }

            if (transaction.IsCoinBase)
            {
                if ((transaction.Inputs[0].ScriptSig.Length < 2) || (transaction.Inputs[0].ScriptSig.Length > 100))
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_SIZE]");
                    ConsensusErrors.BadCoinbaseSize.Throw();
                }
            }
            else
            {
                foreach (TxIn txin in transaction.Inputs)
                {
                    if (txin.PrevOut.IsNull)
                    {
                        this.logger.LogTrace("(-)[TX_NULL_PREVOUT]");
                        ConsensusErrors.BadTransactionNullPrevout.Throw();
                    }
                }
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Checks if value is in range from 0 to <see cref="ConsensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise <c>false</c>.</returns>
        private bool MoneyRange(long value)
        {
            return ((value >= 0) && (value <= this.ConsensusOptions.MaxMoney));
        }

        /// <summary>
        /// Gets the block weight.
        /// </summary>
        /// <remarks>
        /// This implements the <c>weight = (stripped_size * 4) + witness_size</c> formula, using only serialization with and without witness data.
        /// As witness_size is equal to total_size - stripped_size, this formula is identical to: <c>weight = (stripped_size * 3) + total_size</c>.
        /// </remarks>
        /// <param name="block">Block that we get weight of.</param>
        /// <returns>Block weight.</returns>
        public long GetBlockWeight(Block block)
        {
            // Retain legacy static flags
            var stratis = block.Header.NetworkOptions & NetworkOptions.POS;

            // This implements the weight = (stripped_size * 4) + witness_size formula,
            // using only serialization with and without witness data. As witness_size
            // is equal to total_size - stripped_size, this formula is identical to:
            // weight = (stripped_size * 3) + total_size.
            return this.GetSize(block, stratis) * (this.ConsensusOptions.WitnessScaleFactor - 1) + this.GetSize(block, stratis | NetworkOptions.Witness);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        private int GetSize(IBitcoinSerializable data, NetworkOptions options)
        {
            // Retain legacy static flags
            var stratis = options & NetworkOptions.POS;

            var bms = new BitcoinStream(Stream.Null, true);
            bms.NetworkOptions |= options;

            // Propagate legacy static flags.
            if (data is Block)
                bms.NetworkOptions |= ((data as Block).NetworkOptions & NetworkOptions.POS);

            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }

        /// <summary>
        /// Checks if first <paramref name="lenght"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="a">First array.</param>
        /// <param name="b">Second array.</param>
        /// <param name="lenght">Number of entries to be checked.</param>
        /// <returns><c>true</c> if <paramref name="lenght"/> entries are equal between two arrays. Otherwise <c>false</c>.</returns>
        private bool EqualsArray(byte[] a, byte[] b, int lenght)
        {
            for (int i = 0; i < lenght; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Calculates merkle root for witness data.
        /// </summary>
        /// <param name="block">Block which transactions witness data is used for calculation.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public uint256 BlockWitnessMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return this.ComputeMerkleRoot(leaves, out mutated);
        }

        /// <summary>
        /// Calculates merkle root for block's trasnactions.
        /// </summary>
        /// <param name="block">Block which transactions are used for calculation.</param>
        /// <param name="mutated"><c>true</c> if block contains repeating sequences of transactions without affecting the merkle root of a block. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public uint256 BlockMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>(block.Transactions.Count);
            foreach (Transaction tx in block.Transactions)
                leaves.Add(tx.GetHash());

            return this.ComputeMerkleRoot(leaves, out mutated);
        }

        /// <summary>
        /// Computes merkle root.
        /// </summary>
        /// <remarks>This implements a constant-space merkle root/path calculator, limited to 2^32 leaves.</remarks>
        /// <param name="leaves">Merkle tree leaves.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        public uint256 ComputeMerkleRoot(List<uint256> leaves, out bool mutated)
        {
            var branch = new List<uint256>();

            mutated = false;
            if (leaves.Count == 0)
                return uint256.Zero;

            // count is the number of leaves processed so far.
            uint count = 0;

            // inner is an array of eagerly computed subtree hashes, indexed by tree
            // level (0 being the leaves).
            // For example, when count is 25 (11001 in binary), inner[4] is the hash of
            // the first 16 leaves, inner[3] of the next 8 leaves, and inner[0] equal to
            // the last leaf. The other inner entries are undefined.
            var inner = new uint256[32];

            for (int i = 0; i < inner.Length; i++)
                inner[i] = uint256.Zero;
            
            // Which position in inner is a hash that depends on the matching leaf.
            int matchLevel = -1;
            
            // First process all leaves into 'inner' values.
            while (count < leaves.Count)
            {
                uint256 h = leaves[(int)count];
                bool match = false;
                count++;
                int level;
            
                // For each of the lower bits in count that are 0, do 1 step. Each
                // corresponds to an inner value that existed before processing the
                // current leaf, and each needs a hash to combine it.
                for (level = 0; (count & (((uint)1) << level)) == 0; level++)
                {
                    if (branch != null)
                    {
                        if (match)
                        {
                            branch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            branch.Add(h);
                            match = true;
                        }
                    }
                    if (!mutated)
                        mutated = inner[level] == h;
                    var hash = new byte[64];
                    Buffer.BlockCopy(inner[level].ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(h.ToBytes(), 0, hash, 32, 32);
                    h = Hashes.Hash256(hash);
                }

                // Store the resulting hash at inner position level.
                inner[level] = h;
                if (match)
                    matchLevel = level;
            }

            uint256 root;

            {
                // Do a final 'sweep' over the rightmost branch of the tree to process
                // odd levels, and reduce everything to a single top value.
                // Level is the level (counted from the bottom) up to which we've sweeped.
                int level = 0;

                // As long as bit number level in count is zero, skip it. It means there
                // is nothing left at this level.
                while ((count & (((uint)1) << level)) == 0)
                    level++;

                root = inner[level];
                bool match = matchLevel == level;
                while (count != (((uint)1) << level))
                {
                    // If we reach this point, h is an inner value that is not the top.
                    // We combine it with itself (Bitcoin's special rule for odd levels in
                    // the tree) to produce a higher level one.
                    if (match)
                        branch.Add(root);

                    var hash = new byte[64];
                    Buffer.BlockCopy(root.ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(root.ToBytes(), 0, hash, 32, 32);
                    root = Hashes.Hash256(hash);

                    // Increment count to the value it would have if two entries at this
                    // level had existed.
                    count += (((uint)1) << level);
                    level++;

                    // And propagate the result upwards accordingly.
                    while ((count & (((uint)1) << level)) == 0)
                    {
                        if (match)
                        {
                            branch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            branch.Add(root);
                            match = true;
                        }

                        var hashh = new byte[64];
                        Buffer.BlockCopy(inner[level].ToBytes(), 0, hashh, 0, 32);
                        Buffer.BlockCopy(root.ToBytes(), 0, hashh, 32, 32);
                        root = Hashes.Hash256(hashh);

                        level++;
                    }
                }
            }

            return root;
        }

        /// <summary>
        /// Gets index of the last coinbase transaction output with SegWit flag.
        /// </summary>
        /// <param name="block">Block which coinbase transaction's outputs will be checked for SegWit flags.</param>
        /// <returns>
        /// <c>-1</c> if no SegWit flags were found.
        /// If SegWit flag is found index of the last transaction's output that has SegWit flag is returned.
        /// </returns>
        private int GetWitnessCommitmentIndex(Block block)
        {
            int commitpos = -1;
            for (int i = 0; i < block.Transactions[0].Outputs.Count; i++)
            {
                var scriptPubKey = block.Transactions[0].Outputs[i].ScriptPubKey;

                if (scriptPubKey.Length >= 38)
                {
                    byte[] scriptBytes = scriptPubKey.ToBytes(true);

                    if ((scriptBytes[0] == (byte)OpcodeType.OP_RETURN) &&
                        (scriptBytes[1] == 0x24) &&
                        (scriptBytes[2] == 0xaa) &&
                        (scriptBytes[3] == 0x21) &&
                        (scriptBytes[4] == 0xa9) &&
                        (scriptBytes[5] == 0xed))
                    {
                        commitpos = i;
                    }
                }
            }

            return commitpos;
        }

        /// <summary>
        /// Checks if first <paramref name="subset.Lenght"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="bytes">Main array.</param>
        /// <param name="subset">Subset array.</param>
        /// <returns><c>true</c> if <paramref name="subset.Lenght"/> entries are equal between two arrays. Otherwise <c>false</c>.</returns>
        private bool StartWith(byte[] bytes, byte[] subset)
        {
            if (bytes.Length < subset.Length)
                return false;

            for (int i = 0; i < subset.Length; i++)
            {
                if (subset[i] != bytes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Context-dependent validity checks.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <exception cref="ConsensusErrors.BadDiffBits">Thrown if proof of work is incorrect.</exception>
        /// <exception cref="ConsensusErrors.TimeTooOld">Thrown if block's timestamp is too early.</exception>
        /// <exception cref="ConsensusErrors.TimeTooNew">Thrown if block' timestamp too far in the future.</exception>
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        /// <exception cref="ConsensusErrors.CheckpointViolation">Thrown if block header hash does not match the checkpointed value.</exception>
        public virtual void ContextualCheckBlockHeader(ContextInformation context)
        {
            Guard.NotNull(context.BestBlock, nameof(context.BestBlock));
            this.logger.LogTrace("()");

            BlockHeader header = context.BlockValidationContext.Block.Header;

            int height = context.BestBlock.Height + 1;

            // Check proof of work.
            if (header.Bits != context.NextWorkRequired)
            {
                this.logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }

            // Check timestamp against prev.
            if (header.BlockTime <= context.BestBlock.MedianTimePast)
            {
                this.logger.LogTrace("(-)[TIME_TOO_OLD]");
                ConsensusErrors.TimeTooOld.Throw();
            }

            // Check timestamp.
            if (header.BlockTime > (context.Time + TimeSpan.FromHours(2)))
            {
                this.logger.LogTrace("(-)[TIME_TOO_NEW]");
                ConsensusErrors.TimeTooNew.Throw();
            }

            // Reject outdated version blocks when 95% (75% on testnet) of the network has upgraded:
            // check for version 2, 3 and 4 upgrades.
            if (((header.Version < 2) && (height >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34])) ||
               ((header.Version < 3) && (height >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66])) ||
               ((header.Version < 4) && (height >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65])))
            {
                this.logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            // Check that the block header hash matches the known checkpointed value, if any.
            if (!this.Checkpoints.CheckHardened(height, header.GetHash()))
            {
                this.logger.LogTrace("(-)[CHECKPOINT_VIOLATION]");
                ConsensusErrors.CheckpointViolation.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }
    }
}
