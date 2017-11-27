using System;
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
    /// Provides functionality to verify if block is a valid PoW block.
    /// </summary>
    /// <remarks>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</remarks>
    public class PowConsensusValidator
    {
        /// <summary>Used as the flags parameter for checks in non-consensus code.</summary>
        public static Transaction.LockTimeFlags StandardLocktimeVerifyFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of block header hash checkpoints.</summary>
        protected readonly ICheckpoints Checkpoints;

        /// <summary>The consensus parameters.</summary>
        public NBitcoin.Consensus ConsensusParams { get; }

        /// <summary>Consensus options.</summary>
        public PowConsensusOptions ConsensusOptions { get; }

        /// <summary>Keeps track of how much time different actions took to execute and how many times they were executed.</summary>
        public ConsensusPerformanceCounter PerformanceCounter { get; }

        public bool UseConsensusLib { get; set; }

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

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
        /// <param name="context">The context.</param>
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
        /// <param name="context">The context.</param>
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if one or more transactions are not finalized.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseHeight">Thrown if coinbase doesn't start with serialized block height.</exception>
        public virtual void ContextualCheckBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            DeploymentFlags deploymentFlags = context.Flags;

            int nHeight = context.BestBlock == null ? 0 : context.BestBlock.Height + 1;

            // Start enforcing BIP113 (Median Time Past) using versionbits logic.
            DateTimeOffset nLockTimeCutoff = deploymentFlags.LockTimeFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast) ?
                context.BestBlock.MedianTimePast :
                block.Header.BlockTime;

            // Check that all transactions are finalized.
            foreach (Transaction transaction in block.Transactions)
            {
                if (!transaction.IsFinal(nLockTimeCutoff, nHeight))
                {
                    this.logger.LogTrace("(-)[TX_NON_FINAL]");
                    ConsensusErrors.BadTransactionNonFinal.Throw();
                }
            }

            // Enforce rule that the coinbase starts with serialized block height.
            if (deploymentFlags.EnforceBIP34)
            {
                Script expect = new Script(Op.GetPushOp(nHeight));
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
            bool fHaveWitness = false;
            if (deploymentFlags.ScriptFlags.HasFlag(ScriptVerify.Witness))
            {
                int commitpos = this.GetWitnessCommitmentIndex(block);
                if (commitpos != -1)
                {
                    bool malleated = false;
                    uint256 hashWitness = this.BlockWitnessMerkleRoot(block, out malleated);

                    // The malleation check is ignored; as the transaction tree itself
                    // already does not permit it, it is impossible to trigger in the
                    // witness tree.
                    WitScript witness = block.Transactions[0].Inputs[0].WitScript;
                    if ((witness.PushCount != 1) || (witness.Pushes.First().Length != 32))
                    {
                        this.logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        ConsensusErrors.BadWitnessNonceSize.Throw();
                    }

                    byte[] hashed = new byte[64];
                    Buffer.BlockCopy(hashWitness.ToBytes(), 0, hashed, 0, 32);
                    Buffer.BlockCopy(witness.Pushes.First(), 0, hashed, 32, 32);
                    hashWitness = Hashes.Hash256(hashed);

                    if (!this.EqualsArray(hashWitness.ToBytes(), block.Transactions[0].Outputs[commitpos].ScriptPubKey.ToBytes(true).Skip(6).ToArray(), 32))
                    {
                        this.logger.LogTrace("(-)[WITNESS_MERKLE_MISMATCH]");
                        ConsensusErrors.BadWitnessMerkleMatch.Throw();
                    }

                    fHaveWitness = true;
                }
            }

            if (!fHaveWitness)
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
        /// <param name="context">The context.</param>
        /// <param name="taskScheduler">The task scheduler.</param>
        /// <exception cref="ConsensusErrors.BadTransactionBIP30">Thrown if block contain transactions which 'overwrite' older transactions.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction tries to spend inputs that are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if transaction's height or time is lower then provided by SequenceLock for this block.</exception>
        /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if signature operation cost is greater then maximum block signature operation cost.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionScriptError">Thrown if not all inputs are valid (no double spends, scripts & sigs, amounts).</exception>
        public virtual void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler)
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

            long nSigOpsCost = 0;
            Money nFees = Money.Zero;
            List<Task<bool>> checkInputs = new List<Task<bool>>();
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
                    nSigOpsCost += this.GetTransactionSignatureOperationCost(tx, view, flags);
                    if (nSigOpsCost > this.ConsensusOptions.MaxBlockSigopsCost)
                        ConsensusErrors.BadBlockSigOps.Throw();

                    // TODO: Simplify this condition.
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        this.CheckInputs(tx, view, index.Height);
                        nFees += view.GetValueIn(tx) - tx.TotalOut;
                        PrecomputedTransactionData txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            this.PerformanceCounter.AddProcessedInputs(1);
                            TxIn input = tx.Inputs[inputIndex];
                            int inputIndexCopy = inputIndex;
                            TxOut txout = view.GetOutputFor(input);
                            var checkInput = new Task<bool>(() =>
                            {
                                if (this.UseConsensusLib)
                                {
                                    Script.BitcoinConsensusError error;
                                    return Script.VerifyScriptConsensus(txout.ScriptPubKey, tx, (uint)inputIndexCopy, flags.ScriptFlags, out error);
                                }
                                else
                                {
                                    var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                    var ctx = new ScriptEvaluationContext();
                                    ctx.ScriptVerify = flags.ScriptFlags;
                                    return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                                }
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
                this.CheckBlockReward(context, nFees, index, block);

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
        /// Updates context's coin view.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="tx">Transaction that is used for fetching UTXO's.</param>
        protected virtual void UpdateCoinView(ContextInformation context, Transaction tx)
        {
            this.logger.LogTrace("()");

            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            UnspentOutputSet view = context.Set;

            view.Update(tx, index.Height);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Verifies that block has correct coinbase transaction with appropriate reward and fees summ.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="fees">Total amount of fees from transactions that are included in that block.</param>
        /// <param name="chainedBlock">The chained block.</param>
        /// <param name="block">The block.</param>
        /// <exception cref="ConsensusErrors.BadCoinbaseAmount">Thrown if coinbase transaction output value is larger than expected.</exception>
        public virtual void CheckBlockReward(ContextInformation context, Money fees, ChainedBlock chainedBlock, Block block)
        {
            this.logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(chainedBlock.Height);
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
        /// <param name="coins">UTXOs.</param>
        /// <param name="spendHeight">Height at which coins are considered spendable.</param>
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
        /// <param name="tx">The transaction.</param>
        /// <param name="inputs">Inputs for the transaction.</param>
        /// <param name="spendHeight">Height at which coins are considered spendable.</param>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction's inputs are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInputValueOutOfRange">Thrown if input value is out of range.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInBelowOut">Thrown if transaction inputs are less then outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeFee">Thrown if fees sum is negative.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionFeeOutOfRange">Thrown if fees value is out of range.</exception>
        public virtual void CheckInputs(Transaction tx, UnspentOutputSet inputs, int spendHeight)
        {
            this.logger.LogTrace("({0}:{1})", nameof(spendHeight), spendHeight);

            if (!inputs.HaveInputs(tx))
                ConsensusErrors.BadTransactionMissingInput.Throw();

            Money nValueIn = Money.Zero;
            Money nFees = Money.Zero;
            for (int i = 0; i < tx.Inputs.Count; i++)
            {
                OutPoint prevout = tx.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);

                this.CheckMaturity(coins, spendHeight);

                // Check for negative or overflow input values.
                nValueIn += coins.TryGetOutput(prevout.N).Value;
                if (!this.MoneyRange(coins.TryGetOutput(prevout.N).Value) || !this.MoneyRange(nValueIn))
                {
                    this.logger.LogTrace("(-)[BAD_TX_INPUT_VALUE]");
                    ConsensusErrors.BadTransactionInputValueOutOfRange.Throw();
                }
            }

            if (nValueIn < tx.TotalOut)
            {
                this.logger.LogTrace("(-)[TX_IN_BELOW_OUT]");
                ConsensusErrors.BadTransactionInBelowOut.Throw();
            }

            // Tally transaction fees.
            Money nTxFee = nValueIn - tx.TotalOut;
            if (nTxFee < 0)
            {
                this.logger.LogTrace("(-)[NEGATIVE_FEE]");
                ConsensusErrors.BadTransactionNegativeFee.Throw();
            }

            nFees += nTxFee;
            if (!this.MoneyRange(nFees))
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

            Money nSubsidy = this.ConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            nSubsidy >>= halvings;
            return nSubsidy;
        }

        /// <summary>
        /// Calculates total signature operation cost of a transaction.
        /// </summary>
        /// <param name="tx">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for all transaction's inputs.</returns>
        public long GetTransactionSignatureOperationCost(Transaction tx, UnspentOutputSet inputs, DeploymentFlags flags)
        {
            long signatureOperationCost = this.GetLegacySignatureOperationsCount(tx) * this.ConsensusOptions.WitnessScaleFactor;

            if (tx.IsCoinBase)
                return signatureOperationCost;

            if (flags.ScriptFlags.HasFlag(ScriptVerify.P2SH))
            {
                signatureOperationCost += this.GetP2SHSignatureOperationsCount(tx, inputs) * this.ConsensusOptions.WitnessScaleFactor;
            }

            for (int i = 0; i < tx.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(tx.Inputs[i]);
                signatureOperationCost += this.CountWitnessSignatureOperation(tx.Inputs[i].ScriptSig, prevout.ScriptPubKey, tx.Inputs[i].WitScript, flags);
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
            if (witParams != null)
                return this.WitnessSignatureOperation(witParams, witness, flags);

            if (scriptPubKey.IsPayToScriptHash && scriptSig.IsPushOnly)
            {
                witParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(scriptPubKey);
                if (witParams != null)
                    return this.WitnessSignatureOperation(witParams, witness, flags);
            }

            return 0;
        }

        private long WitnessSignatureOperation(WitProgramParameters witParams, WitScript witScript, DeploymentFlags flags)
        {
            if (witParams.Version == 0)
            {
                if (witParams.Program.Length == 20)
                    return 1;

                if (witParams.Program.Length == 32 && witScript.PushCount > 0)
                {
                    Script subscript = Script.FromBytesUnsafe(witScript.GetUnsafePush(witScript.PushCount - 1));
                    return subscript.GetSigOpCount(true);
                }
            }

            // Future flags may be implemented here.
            return 0;
        }

        /// <summary>
        /// Calculates pay-to-script-hash (BIP16) transaction signature operation cost.
        /// </summary>
        /// <param name="tx">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private uint GetP2SHSignatureOperationsCount(Transaction tx, UnspentOutputSet inputs)
        {
            if (tx.IsCoinBase)
                return 0;

            uint nSigOps = 0;
            for (int i = 0; i < tx.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(tx.Inputs[i]);
                if (prevout.ScriptPubKey.IsPayToScriptHash)
                    nSigOps += prevout.ScriptPubKey.GetSigOpCount(tx.Inputs[i].ScriptSig);
            }

            return nSigOps;
        }

        /// <summary>
        /// Checks block's validity.
        /// </summary>
        /// <param name="context">Context.</param>
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

            bool mutated = false;
            uint256 hashMerkleRoot2 = this.BlockMerkleRoot(block, ref mutated);
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

            // Size limits.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > this.ConsensusOptions.MaxBlockBaseSize) || (this.GetSize(block, TransactionOptions.None) > this.ConsensusOptions.MaxBlockBaseSize))
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

            long nSigOps = 0;
            foreach (Transaction tx in block.Transactions)
                nSigOps += this.GetLegacySignatureOperationsCount(tx);

            if ((nSigOps * this.ConsensusOptions.WitnessScaleFactor) > this.ConsensusOptions.MaxBlockSigopsCost)
            {
                this.logger.LogTrace("(-)[BAD_BLOCK_SIGOPS]");
                ConsensusErrors.BadBlockSigOps.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Calculates legacy transaction signature operation cost.
        /// </summary>
        /// <param name="tx">Transaction for which we are computing the cost.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private long GetLegacySignatureOperationsCount(Transaction tx)
        {
            long nSigOps = 0;
            foreach (TxIn txin in tx.Inputs)
                nSigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in tx.Outputs)
                nSigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return nSigOps;
        }

        /// <summary>
        /// Checks if transaction is valid.
        /// </summary>
        /// <param name="tx">Transaction.</param>
        /// <exception cref="ConsensusErrors.BadTransactionNoInput">Thrown if transaction has no inputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNoOutput">Thrown if transaction has no outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionOversize">Thrown if transaction size is greater than maximum allowed size of a block.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeOutput">Thrown if at least one transaction output has negative value.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeOutput">Thrown if at least one transaction output value is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionTooLargeTotalOutput">Thrown if sum of all transaction outputs is greater than maximum allowed one.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionDuplicateInputs">Thrown if any of transaction inputs are duplicate.</exception>
        /// <exception cref="ConsensusErrors.BadCoinbaseSize">Thrown if coinbase transaction is too small or too big.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNullPrevout">Thrown if transaction contains a null prevout.</exception>
        public virtual void CheckTransaction(Transaction tx)
        {
            this.logger.LogTrace("()");

            // Basic checks that don't depend on any context.
            if (tx.Inputs.Count == 0)
            {
                this.logger.LogTrace("(-)[TX_NO_INPUT]");
                ConsensusErrors.BadTransactionNoInput.Throw();
            }

            if (tx.Outputs.Count == 0)
            {
                this.logger.LogTrace("(-)[TX_NO_OUTPUT]");
                ConsensusErrors.BadTransactionNoOutput.Throw();
            }

            // Size limits (this doesn't take the witness into account, as that hasn't been checked for malleability).
            if (this.GetSize(tx, TransactionOptions.None) > this.ConsensusOptions.MaxBlockBaseSize)
            {
                this.logger.LogTrace("(-)[TX_OVERSIZE]");
                ConsensusErrors.BadTransactionOversize.Throw();
            }

            // Check for negative or overflow output values
            long nValueOut = 0;
            foreach (TxOut txout in tx.Outputs)
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

                nValueOut += txout.Value;
                if (!this.MoneyRange(nValueOut))
                {
                    this.logger.LogTrace("(-)[TX_TOTAL_OUTPUT_TOO_LARGE]");
                    ConsensusErrors.BadTransactionTooLargeTotalOutput.Throw();
                }
            }

            // Check for duplicate inputs.
            HashSet<OutPoint> vInOutPoints = new HashSet<OutPoint>();
            foreach (TxIn txin in tx.Inputs)
            {
                if (vInOutPoints.Contains(txin.PrevOut))
                {
                    this.logger.LogTrace("(-)[TX_DUP_INPUTS]");
                    ConsensusErrors.BadTransactionDuplicateInputs.Throw();
                }

                vInOutPoints.Add(txin.PrevOut);
            }

            if (tx.IsCoinBase)
            {
                if ((tx.Inputs[0].ScriptSig.Length < 2) || (tx.Inputs[0].ScriptSig.Length > 100))
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_SIZE]");
                    ConsensusErrors.BadCoinbaseSize.Throw();
                }
            }
            else
            {
                foreach (TxIn txin in tx.Inputs)
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
        /// Checks if value is in range from 0 to <see cref="this.consensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise: <c>false</c>.</returns>
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
        /// <param name="block">The block.</param>
        /// <returns>Block weight.</returns>
        public long GetBlockWeight(Block block)
        {
            return this.GetSize(block, TransactionOptions.None) * (this.ConsensusOptions.WitnessScaleFactor - 1) + this.GetSize(block, TransactionOptions.Witness);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns></returns>
        public int GetSize(IBitcoinSerializable data, TransactionOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }

        /// <summary>
        /// Checks if first <paramref name="len"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="a">First array.</param>
        /// <param name="b">Second array.</param>
        /// <param name="len">Number of entries to be checked.</param>
        /// <returns><c>true</c> if <paramref name="len"/> entries are equal between two arrays. Otherwise: <c>false</c>.</returns>
        private bool EqualsArray(byte[] a, byte[] b, int len)
        {
            for (int i = 0; i < len; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        private uint256 BlockWitnessMerkleRoot(Block block, out bool mutated)
        {
            List<uint256> leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return this.ComputeMerkleRoot(leaves, out mutated);
        }

        private uint256 BlockMerkleRoot(Block block, ref bool mutated)
        {
            List<uint256> leaves = new List<uint256>(block.Transactions.Count);
            for (int s = 0; s < block.Transactions.Count; s++)
                leaves.Add(block.Transactions[s].GetHash());

            return this.ComputeMerkleRoot(leaves, out mutated);
        }

        private uint256 ComputeMerkleRoot(List<uint256> leaves, out bool mutated)
        {
            uint256 hash = null;
            this.MerkleComputation(leaves, ref hash, out mutated, -1, null);
            return hash;
        }

        /// <summary>
        /// This implements a constant-space merkle root/path calculator, limited to 2^32 leaves.
        /// </summary>
        private void MerkleComputation(List<uint256> leaves, ref uint256 root, out bool pmutated, int branchpos, List<uint256> branch)
        {
            if (branch != null)
                branch.Clear();

            if (leaves.Count == 0)
            {
                pmutated = false;
                root = uint256.Zero;
                return;
            }

            bool mutated = false;

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
                bool matchh = count == branchpos;
                count++;
                int level;

                // For each of the lower bits in count that are 0, do 1 step. Each
                // corresponds to an inner value that existed before processing the
                // current leaf, and each needs a hash to combine it.
                for (level = 0; (count & (((uint)1) << level)) == 0; level++)
                {
                    if (branch != null)
                    {
                        if (matchh)
                        {
                            branch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            branch.Add(h);
                            matchh = true;
                        }
                    }
                    mutated |= (inner[level] == h);
                    var hash = new byte[64];
                    Buffer.BlockCopy(inner[level].ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(h.ToBytes(), 0, hash, 32, 32);
                    h = Hashes.Hash256(hash);
                }

                // Store the resulting hash at inner position level.
                inner[level] = h;
                if (matchh)
                    matchLevel = level;
            }

            // Do a final 'sweep' over the rightmost branch of the tree to process
            // odd levels, and reduce everything to a single top value.
            // Level is the level (counted from the bottom) up to which we've sweeped.
            int levell = 0;

            // As long as bit number level in count is zero, skip it. It means there
            // is nothing left at this level.
            while ((count & (((uint)1) << levell)) == 0)
                levell++;

            uint256 hh = inner[levell];
            bool matchhh = matchLevel == levell;
            while (count != (((uint)1) << levell))
            {
                // If we reach this point, h is an inner value that is not the top.
                // We combine it with itself (Bitcoin's special rule for odd levels in
                // the tree) to produce a higher level one.
                if (branch != null && matchhh)
                    branch.Add(hh);

                var hash = new byte[64];
                Buffer.BlockCopy(hh.ToBytes(), 0, hash, 0, 32);
                Buffer.BlockCopy(hh.ToBytes(), 0, hash, 32, 32);
                hh = Hashes.Hash256(hash);

                // Increment count to the value it would have if two entries at this
                // level had existed.
                count += (((uint)1) << levell);
                levell++;

                // And propagate the result upwards accordingly.
                while ((count & (((uint)1) << levell)) == 0)
                {
                    if (branch != null)
                    {
                        if (matchhh)
                        {
                            branch.Add(inner[levell]);
                        }
                        else if (matchLevel == levell)
                        {
                            branch.Add(hh);
                            matchhh = true;
                        }
                    }

                    var hashh = new byte[64];
                    Buffer.BlockCopy(inner[levell].ToBytes(), 0, hashh, 0, 32);
                    Buffer.BlockCopy(hh.ToBytes(), 0, hashh, 32, 32);
                    hh = Hashes.Hash256(hashh);

                    levell++;
                }
            }
            // Return result.
            pmutated = mutated;
            root = hh;
        }

        private int GetWitnessCommitmentIndex(Block block)
        {
            int commitpos = -1;
            for (int i = 0; i < block.Transactions[0].Outputs.Count; i++)
            {
                var scriptPubKey = block.Transactions[0].Outputs[i].ScriptPubKey;
                byte[] scriptBytes = scriptPubKey.ToBytes(true);

                if ((scriptPubKey.Length >= 38) &&
                    (scriptBytes[0] == (byte)OpcodeType.OP_RETURN) &&
                    (scriptBytes[1] == 0x24) &&
                    (scriptBytes[2] == 0xaa) &&
                    (scriptBytes[3] == 0x21) &&
                    (scriptBytes[4] == 0xa9) &&
                    (scriptBytes[5] == 0xed))
                {
                    commitpos = i;
                }
            }

            return commitpos;
        }

        /// <summary>
        /// Checks if first <paramref name="subset.Lenght"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="bytes">Main array.</param>
        /// <param name="subset">Subset array.</param>
        /// <returns><c>true</c> if <paramref name="subset.Lenght"/> entries are equal between two arrays. Otherwise: <c>false</c>.</returns>
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
        /// <param name="context">Context.</param>
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

            int nHeight = context.BestBlock.Height + 1;

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
            if (((header.Version < 2) && (nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34])) ||
               ((header.Version < 3) && (nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66])) ||
               ((header.Version < 4) && (nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65])))
            {
                this.logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            // Check that the block header hash matches the known checkpointed value, if any.
            if (!this.Checkpoints.CheckHardened(nHeight, header.GetHash()))
            {
                this.logger.LogTrace("(-)[CHECKPOINT_VIOLATION]");
                ConsensusErrors.CheckpointViolation.Throw();
            }

            this.logger.LogTrace("(-)[OK]");
        }
    }
}
