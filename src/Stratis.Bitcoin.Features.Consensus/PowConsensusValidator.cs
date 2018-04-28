using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Provides functionality for verifying validity of PoW block.
    /// </summary>
    /// <remarks>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</remarks>
    public class PowConsensusValidator : IPowConsensusValidator
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

        /// <inheritdoc />
        public virtual void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null)
        {
            this.logger.LogTrace("()");

            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.PerformanceCounter.AddProcessedBlocks(1);
            taskScheduler = taskScheduler ?? TaskScheduler.Default;

            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();
            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                this.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.SkipValidation)
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

            if (!context.SkipValidation)
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
        protected virtual void UpdateCoinView(RuleContext context, Transaction transaction)
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
        protected virtual void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
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
        /// Checks if value is in range from 0 to <see cref="ConsensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise <c>false</c>.</returns>
        private bool MoneyRange(long value)
        {
            return ((value >= 0) && (value <= this.ConsensusOptions.MaxMoney));
        }

        /// <inheritdoc />
        public long GetBlockWeight(Block block)
        {
            var options = NetworkOptions.TemporaryOptions;
            return this.GetSize(block, options & ~NetworkOptions.Witness) * (this.ConsensusOptions.WitnessScaleFactor - 1) +
                   this.GetSize(block, options | NetworkOptions.Witness);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        private int GetSize(IBitcoinSerializable data, NetworkOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
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

        /// <inheritdoc />
        public uint256 BlockWitnessMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }

        /// <inheritdoc />
        public uint256 BlockMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>(block.Transactions.Count);
            foreach (Transaction tx in block.Transactions)
                leaves.Add(tx.GetHash());

            return BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
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
    }
}
