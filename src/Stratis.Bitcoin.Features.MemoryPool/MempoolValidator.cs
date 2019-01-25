using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Public interface for the memory pool validator.
    /// </summary>
    public interface IMempoolValidator
    {
        /// <summary>Gets the proof of work consensus option.</summary>
        ConsensusOptions ConsensusOptions { get; }

        /// <summary>Gets the memory pool performance counter.</summary>
        MempoolPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Accept transaction to memory pool.
        /// Sets the validation state accept time to now.
        /// </summary>
        /// <param name="state">Validation state.</param>
        /// <param name="tx">Transaction to accept.</param>
        /// <returns>Whether the transaction is accepted or not.</returns>
        Task<bool> AcceptToMemoryPool(MempoolValidationState state, Transaction tx);

        /// <summary>
        /// Accept transaction to memory pool.
        /// Honors the validation state accept time.
        /// </summary>
        /// <param name="state">Validation state.</param>
        /// <param name="tx">Transaction to accept.</param>
        /// <returns>Whether the transaction was accepted to the memory pool.</returns>
        Task<bool> AcceptToMemoryPoolWithTime(MempoolValidationState state, Transaction tx);

        /// <summary>
        /// Executes the memory pool sanity check here <see cref="TxMempool.Check(CoinView)"/>.
        /// </summary>
        Task SanityCheck();
    }

    /// <summary>
    /// Validates memory pool transactions.
    /// </summary>
    public class MempoolValidator : IMempoolValidator
    {
        /// <summary>
        /// Default for relay priority.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const bool DefaultRelaypriority = true;

        /// <summary>
        /// Default for -maxmempool, maximum megabytes of mempool memory usage.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultMaxMempoolSize = 300;

        /// <summary>
        /// Default limit free relay.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultLimitfreerelay = 0;

        /// <summary>
        /// Default for -limitancestorcount, max number of in-mempool ancestors.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultAncestorLimit = 25;

        /// <summary>
        /// Default for -limitancestorsize, maximum kilobytes of tx + all in-mempool ancestors.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultAncestorSizeLimit = 101;

        /// <summary>
        /// Default for -limitdescendantcount, max number of in-mempool descendants.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultDescendantLimit = 25;

        /// <summary>
        /// Default for -limitdescendantsize, maximum kilobytes of in-mempool descendants.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultDescendantSizeLimit = 101;

        /// <summary>
        /// Default for -mempoolexpiry, expiration time for mempool transactions in hours.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const int DefaultMempoolExpiry = 336;

        /// <summary>
        /// Default for -mempoolreplacement, whether to replace memory pool.
        /// </summary>
        /// <seealso cref = "MempoolSettings" />
        public const bool DefaultEnableReplacement = true;

        /// <summary>Maximum age of our tip in seconds for us to be considered current for fee estimation.</summary>
        private const int MaxFeeEstimationTipAge = 3 * 60 * 60;

        /// <summary>A lock for managing asynchronous access to memory pool.</summary>
        private readonly MempoolSchedulerLock mempoolLock;

        /// <summary>Date and time information provider.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Settings from the memory pool.</summary>
        private readonly MempoolSettings mempoolSettings;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Coin view of the memory pool.</summary>
        private readonly ICoinView coinView;

        /// <inheritdoc cref="IConsensusRuleEngine" />
        private readonly IConsensusRuleEngine consensusRules;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        private readonly ITxMempool memPool;

        /// <summary>Instance logger for memory pool validator.</summary>
        private readonly ILogger logger;

        /// <summary>Minimum fee rate for a relay transaction.</summary>
        private readonly FeeRate minRelayTxFee;

        /// <summary>Flags that determine how transaction should be validated in non-consensus code.</summary>
        public static Transaction.LockTimeFlags StandardLocktimeVerifyFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

        // TODO: Implement Later with CheckRateLimit()
        //private readonly FreeLimiterSection freeLimiter;

        //private class FreeLimiterSection
        //{
        //  public double FreeCount;
        //  public long LastTime;
        //}

        private Network network;

        public MempoolValidator(
            ITxMempool memPool,
            MempoolSchedulerLock mempoolLock,
            IDateTimeProvider dateTimeProvider,
            MempoolSettings mempoolSettings,
            ConcurrentChain chain,
            ICoinView coinView,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IConsensusRuleEngine consensusRules)
        {
            this.memPool = memPool;
            this.mempoolLock = mempoolLock;
            this.dateTimeProvider = dateTimeProvider;
            this.mempoolSettings = mempoolSettings;
            this.chain = chain;
            this.network = chain.Network;
            this.coinView = coinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            // TODO: Implement later with CheckRateLimit()
            // this.freeLimiter = new FreeLimiterSection();
            this.PerformanceCounter = new MempoolPerformanceCounter(this.dateTimeProvider);
            this.minRelayTxFee = nodeSettings.MinRelayTxFeeRate;
            this.consensusRules = consensusRules;
        }

        /// <summary>Gets a counter for tracking memory pool performance.</summary>
        public MempoolPerformanceCounter PerformanceCounter { get; }

        /// <summary>Gets the consensus options from the <see cref="CoinViewRule"/></summary>
        public ConsensusOptions ConsensusOptions => this.network.Consensus.Options;

        /// <inheritdoc />
        public async Task<bool> AcceptToMemoryPoolWithTime(MempoolValidationState state, Transaction tx)
        {
            try
            {
                var vHashTxToUncache = new List<uint256>();
                await this.AcceptToMemoryPoolWorkerAsync(state, tx, vHashTxToUncache);
                //if (!res) {
                //    BOOST_FOREACH(const uint256& hashTx, vHashTxToUncache)
                //        pcoinsTip->Uncache(hashTx);
                //}
                this.logger.LogTrace("(-):true");
                return true;
            }
            catch (MempoolErrorException mempoolError)
            {
                this.logger.LogTrace("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(MempoolErrorException), mempoolError.Message, mempoolError.ValidationState?.Error?.Code, mempoolError.ValidationState?.ErrorMessage);
                this.logger.LogTrace("(-)[MEMPOOL_EXCEPTION]:false");
                return false;
            }
            catch (ConsensusErrorException consensusError)
            {
                this.logger.LogTrace("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(ConsensusErrorException), consensusError.Message, consensusError.ConsensusError?.Code, consensusError.ConsensusError?.Message);
                state.Error = new MempoolError(consensusError.ConsensusError);
                this.logger.LogTrace("(-)[CONSENSUS_EXCEPTION]:false");
                return false;
            }
        }

        /// <inheritdoc />
        public Task<bool> AcceptToMemoryPool(MempoolValidationState state, Transaction tx)
        {
            state.AcceptTime = this.dateTimeProvider.GetTime();
            return this.AcceptToMemoryPoolWithTime(state, tx);
        }

        /// <inheritdoc />
        public Task SanityCheck()
        {
            return this.mempoolLock.ReadAsync(() => this.memPool.Check(this.coinView));
        }

        /// <summary>
        /// Validates that the transaction is the final transaction."/>
        /// Validated by comparing the transaction vs chain tip.
        /// If <see cref="CoinViewRule.StandardLocktimeVerifyFlags"/> flag is set then
        /// use the block time at the end of the block chain for validation.
        /// Otherwise use the current time for the block time.
        /// </summary>
        /// <param name="chain">Block chain used for computing time-locking on the transaction.</param>
        /// <param name="dateTimeProvider">Provides the current date and time.</param>
        /// <param name="tx">The transaction to validate.</param>
        /// <param name="flags">Flags for time-locking the transaction.</param>
        /// <returns>Whether the final transaction was valid.</returns>
        /// <seealso cref="Transaction.IsFinal(DateTimeOffset, int)"/>
        public static bool CheckFinalTransaction(ConcurrentChain chain, IDateTimeProvider dateTimeProvider, Transaction tx, Transaction.LockTimeFlags flags)
        {
            // By convention a negative value for flags indicates that the
            // current network-enforced consensus rules should be used. In
            // a future soft-fork scenario that would mean checking which
            // rules would be enforced for the next block and setting the
            // appropriate flags. At the present time no soft-forks are
            // scheduled, so no flags are set.
            flags = (Transaction.LockTimeFlags)Math.Max((int)flags, (int)Transaction.LockTimeFlags.None);

            // CheckFinalTx() uses chainActive.Height()+1 to evaluate
            // nLockTime because when IsFinalTx() is called within
            // CBlock::AcceptBlock(), the height of the block *being*
            // evaluated is what is used. Thus if we want to know if a
            // transaction can be part of the *next* block, we need to call
            // IsFinalTx() with one more than chainActive.Height().
            int blockHeight = chain.Height + 1;

            // BIP113 will require that time-locked transactions have nLockTime set to
            // less than the median time of the previous block they're contained in.
            // When the next block is created its previous block will be the current
            // chain tip, so we use that to calculate the median time passed to
            // IsFinalTx() if LOCKTIME_MEDIAN_TIME_PAST is set.
            DateTimeOffset blockTime = flags.HasFlag(StandardLocktimeVerifyFlags)
                ? chain.Tip.Header.BlockTime
                : DateTimeOffset.FromUnixTimeMilliseconds(dateTimeProvider.GetTime());

            return tx.IsFinal(blockTime, blockHeight);
        }

        /// <summary>
        /// Check if transaction will be BIP 68 final in the next block to be created.
        /// Simulates calling SequenceLocks() with data from the tip of the current active chain.
        /// Optionally stores in LockPoints the resulting height and time calculated and the hash
        /// of the block needed for calculation or skips the calculation and uses the LockPoints
        /// passed in for evaluation.
        /// The LockPoints should not be considered valid if CheckSequenceLocks returns false.
        /// See consensus/consensus.h for flag definitions.
        /// </summary>
        /// <param name="network">The blockchain network.</param>
        /// <param name="tip">Tip of the blockchain.</param>
        /// <param name="context">Validation context for the memory pool.</param>
        /// <param name="flags">Transaction lock time flags.</param>
        /// <param name="lp">Optional- existing lock points to use, and update during evaluation.</param>
        /// <param name="useExistingLockPoints">Whether to use the existing lock points during evaluation.</param>
        /// <returns>Whether sequence lock validated.</returns>
        /// <seealso cref="SequenceLock.Evaluate(ChainedHeader)"/>
        public static bool CheckSequenceLocks(Network network, ChainedHeader tip, MempoolValidationContext context, Transaction.LockTimeFlags flags, LockPoints lp = null, bool useExistingLockPoints = false)
        {
            Block dummyBlock = network.Consensus.ConsensusFactory.CreateBlock();
            dummyBlock.Header.HashPrevBlock = tip.HashBlock;
            var index = new ChainedHeader(dummyBlock.Header, dummyBlock.GetHash(), tip);

            // CheckSequenceLocks() uses chainActive.Height()+1 to evaluate
            // height based locks because when SequenceLocks() is called within
            // ConnectBlock(), the height of the block *being*
            // evaluated is what is used.
            // Thus if we want to know if a transaction can be part of the
            // *next* block, we need to use one more than chainActive.Height()

            SequenceLock lockPair;
            if (useExistingLockPoints)
            {
                Guard.Assert(lp != null);
                lockPair = new SequenceLock(lp.Height, lp.Time);
            }
            else
            {
                // pcoinsTip contains the UTXO set for chainActive.Tip()
                var prevheights = new List<int>();
                foreach (TxIn txin in context.Transaction.Inputs)
                {
                    UnspentOutputs coins = context.View.GetCoins(txin.PrevOut.Hash);
                    if (coins == null)
                        return false;

                    if (coins.Height == TxMempool.MempoolHeight)
                    {
                        // Assume all mempool transaction confirm in the next block
                        prevheights.Add(tip.Height + 1);
                    }
                    else
                    {
                        prevheights.Add((int)coins.Height);
                    }
                }
                lockPair = context.Transaction.CalculateSequenceLocks(prevheights.ToArray(), index, flags);

                if (lp != null)
                {
                    lp.Height = lockPair.MinHeight;
                    lp.Time = lockPair.MinTime.ToUnixTimeMilliseconds();
                    // Also store the hash of the block with the highest height of
                    // all the blocks which have sequence locked prevouts.
                    // This hash needs to still be on the chain
                    // for these LockPoint calculations to be valid
                    // Note: It is impossible to correctly calculate a maxInputBlock
                    // if any of the sequence locked inputs depend on unconfirmed txs,
                    // except in the special case where the relative lock time/height
                    // is 0, which is equivalent to no sequence lock. Since we assume
                    // input height of tip+1 for mempool txs and test the resulting
                    // lockPair from CalculateSequenceLocks against tip+1.  We know
                    // EvaluateSequenceLocks will fail if there was a non-zero sequence
                    // lock on a mempool input, so we can use the return value of
                    // CheckSequenceLocks to indicate the LockPoints validity
                    int maxInputHeight = 0;
                    foreach (int height in prevheights)
                    {
                        // Can ignore mempool inputs since we'll fail if they had non-zero locks
                        if (height != tip.Height + 1)
                        {
                            maxInputHeight = Math.Max(maxInputHeight, height);
                        }
                    }

                    lp.MaxInputBlock = tip.GetAncestor(maxInputHeight);
                }
            }

            return lockPair.Evaluate(index);
        }

        /// <summary>
        /// Computes the transaction size based on <see cref="ConsensusOptions"/>.
        /// Takes into account witness options in the computation.
        /// </summary>
        /// <param name="tx">Transaction.</param>
        /// <param name="consensusOptions">Proof of work consensus options.</param>
        /// <returns>Transaction weight.</returns>
        /// <seealso cref="Transaction.GetSerializedSize"/>
        public static int GetTransactionWeight(Transaction tx, ConsensusOptions consensusOptions)
        {
            return tx.GetSerializedSize(
                       (ProtocolVersion)
                       ((uint)ProtocolVersion.PROTOCOL_VERSION | ConsensusOptions.SerializeTransactionNoWitness),
                       SerializationType.Network) * (consensusOptions.WitnessScaleFactor - 1) +
                   tx.GetSerializedSize(ProtocolVersion.PROTOCOL_VERSION, SerializationType.Network);
        }

        /// <summary>
        /// Calculates the modified transaction size used for memory pool priority.
        /// Calculated by stripping off the lengths of the inputs signatures.
        /// </summary>
        /// <param name="nTxSize">Current transaction size, set to 0 to compute it.</param>
        /// <param name="trx">The transaction.</param>
        /// <param name="consensusOptions">The consensus option, needed to compute the transaction size.</param>
        /// <returns>The new transaction size.</returns>
        public static int CalculateModifiedSize(int nTxSize, Transaction trx, ConsensusOptions consensusOptions)
        {
            // In order to avoid disincentivizing cleaning up the UTXO set we don't count
            // the constant overhead for each txin and up to 110 bytes of scriptSig (which
            // is enough to cover a compressed pubkey p2sh redemption) for priority.
            // Providing any more cleanup incentive than making additional inputs free would
            // risk encouraging people to create junk outputs to redeem later.
            if (nTxSize == 0)
                nTxSize = (GetTransactionWeight(trx, consensusOptions) + (consensusOptions.WitnessScaleFactor) - 1) / consensusOptions.WitnessScaleFactor;

            foreach (TxIn txInput in trx.Inputs)
            {
                long offset = 41U + Math.Min(110U, txInput.ScriptSig.Length);
                if (nTxSize > offset)
                    nTxSize -= (int)offset;
            }
            return nTxSize;
        }

        /// <summary>
        /// Validates and then adds a transaction to memory pool.
        /// </summary>
        /// <param name="state">Validation state for creating the validation context.</param>
        /// <param name="tx">The transaction to validate.</param>
        /// <param name="vHashTxnToUncache">Not currently used</param>
        private async Task AcceptToMemoryPoolWorkerAsync(MempoolValidationState state, Transaction tx, List<uint256> vHashTxnToUncache)
        {
            var context = new MempoolValidationContext(tx, state);

            this.PreMempoolChecks(context);

            // create the MemPoolCoinView and load relevant utxoset
            context.View = new MempoolCoinView(this.coinView, this.memPool, this.mempoolLock, this);
            await context.View.LoadViewAsync(context.Transaction).ConfigureAwait(false);

            // adding to the mem pool can only be done sequentially
            // use the sequential scheduler for that.
            await this.mempoolLock.WriteAsync(() =>
            {
                // is it already in the memory pool?
                if (this.memPool.Exists(context.TransactionHash))
                {
                    this.logger.LogTrace("(-)[INVALID_ALREADY_EXISTS]");
                    state.Invalid(MempoolErrors.InPool).Throw();
                }

                // Check for conflicts with in-memory transactions
                this.CheckConflicts(context);

                this.CheckMempoolCoinView(context);

                this.CreateMempoolEntry(context, state.AcceptTime);
                this.CheckSigOps(context);
                this.CheckFee(context);

                this.CheckRateLimit(context, state.LimitFree);

                this.CheckAncestors(context);
                this.CheckReplacment(context);
                this.CheckAllInputs(context);

                // Remove conflicting transactions from the mempool
                foreach (TxMempoolEntry it in context.AllConflicting)
                    this.logger.LogInformation($"replacing tx {it.TransactionHash} with {context.TransactionHash} for {context.ModifiedFees - context.ConflictingFees} BTC additional fees, {context.EntrySize - context.ConflictingSize} delta bytes");

                this.memPool.RemoveStaged(context.AllConflicting, false);

                // This transaction should only count for fee estimation if
                // the node is not behind and it is not dependent on any other
                // transactions in the mempool
                bool validForFeeEstimation = this.IsCurrentForFeeEstimation() && this.memPool.HasNoInputsOf(tx);

                // Store transaction in memory
                this.memPool.AddUnchecked(context.TransactionHash, context.Entry, context.SetAncestors, validForFeeEstimation);

                // trim mempool and check if tx was trimmed
                if (!state.OverrideMempoolLimit)
                {
                    this.LimitMempoolSize(this.mempoolSettings.MaxMempool * 1000000, this.mempoolSettings.MempoolExpiry * 60 * 60);

                    if (!this.memPool.Exists(context.TransactionHash))
                    {
                        this.logger.LogTrace("(-)[FAIL_MEMPOOL_FULL]");
                        state.Fail(MempoolErrors.Full).Throw();
                    }
                }

                // do this here inside the exclusive scheduler for better accuracy
                // and to avoid springing more concurrent tasks later
                state.MempoolSize = this.memPool.Size;
                state.MempoolDynamicSize = this.memPool.DynamicMemoryUsage();

                this.PerformanceCounter.SetMempoolSize(state.MempoolSize);
                this.PerformanceCounter.SetMempoolDynamicSize(state.MempoolDynamicSize);
                this.PerformanceCounter.AddHitCount(1);
            });
        }

        /// <summary>
        /// Check for conflicts with in-memory transactions.
        /// If a conflict is found it is added to the validation context.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        private void CheckConflicts(MempoolValidationContext context)
        {
            context.SetConflicts = new List<uint256>();
            foreach (TxIn txin in context.Transaction.Inputs)
            {
                TxMempool.NextTxPair itConflicting = this.memPool.MapNextTx.Find(f => f.OutPoint == txin.PrevOut);
                if (itConflicting != null)
                {
                    Transaction ptxConflicting = itConflicting.Transaction;
                    if (!context.SetConflicts.Contains(ptxConflicting.GetHash()))
                    {
                        // Allow opt-out of transaction replacement by setting
                        // nSequence >= maxint-1 on all inputs.
                        //
                        // maxint-1 is picked to still allow use of nLockTime by
                        // non-replaceable transactions. All inputs rather than just one
                        // is for the sake of multi-party protocols, where we don't
                        // want a single party to be able to disable replacement.
                        //
                        // The opt-out ignores descendants as anyone relying on
                        // first-seen mempool behavior should be checking all
                        // unconfirmed ancestors anyway; doing otherwise is hopelessly
                        // insecure.
                        bool replacementOptOut = true;
                        if (this.mempoolSettings.EnableReplacement)
                        {
                            foreach (TxIn txiner in ptxConflicting.Inputs)
                            {
                                if (txiner.Sequence < Sequence.Final - 1)
                                {
                                    replacementOptOut = false;
                                    break;
                                }
                            }
                        }

                        if (replacementOptOut)
                        {
                            this.logger.LogTrace("(-)[INVALID_CONFLICT]");
                            context.State.Invalid(MempoolErrors.Conflict).Throw();
                        }

                        context.SetConflicts.Add(ptxConflicting.GetHash());
                    }
                }
            }
        }

        /// <summary>
        /// Checks that are done before touching the memory pool.
        /// These checks don't need to run under the memory pool lock.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        protected virtual void PreMempoolChecks(MempoolValidationContext context)
        {
            // TODO: fix this to use dedicated mempool rules.
            new CheckPowTransactionRule { Logger = this.logger }.CheckTransaction(this.network, this.ConsensusOptions, context.Transaction);
            if (this.chain.Network.Consensus.IsProofOfStake)
                new CheckPosTransactionRule { Logger = this.logger }.CheckTransaction(context.Transaction);

            // Coinbase is only valid in a block, not as a loose transaction
            if (context.Transaction.IsCoinBase)
            {
                this.logger.LogTrace("(-)[FAIL_INVALID_COINBASE]");
                context.State.Fail(MempoolErrors.Coinbase).Throw();
            }

            // Coinstake is only valid in a block, not as a loose transaction
            // TODO: mempool needs to have seprate checks for POW/POS as part of the change to rules.
            if (this.network.Consensus.IsProofOfStake && context.Transaction.IsCoinStake)
            {
                this.logger.LogTrace("(-)[FAIL_INVALID_COINSTAKE]");
                context.State.Fail(MempoolErrors.Coinstake).Throw();
            }

            // TODO: Implement Witness Code
            // Bitcoin Ref: https://github.com/bitcoin/bitcoin/blob/ea729d55b4dbd17a53ced474a8457d4759cfb5a5/src/validation.cpp#L463-L467
            //// Reject transactions with witness before segregated witness activates (override with -prematurewitness)
            bool witnessEnabled = false;//IsWitnessEnabled(chainActive.Tip(), Params().GetConsensus());
            //if (!GetBoolArg("-prematurewitness",false) && tx.HasWitness() && !witnessEnabled) {
            //    return state.DoS(0, false, REJECT_NONSTANDARD, "no-witness-yet", true);
            //}

            // Rather not work on nonstandard transactions (unless -testnet/-regtest)
            if (this.mempoolSettings.RequireStandard)
            {
                this.CheckStandardTransaction(context, witnessEnabled);
            }

            // Only accept nLockTime-using transactions that can be mined in the next
            // block; we don't want our mempool filled up with transactions that can't
            // be mined yet.
            if (!CheckFinalTransaction(this.chain, this.dateTimeProvider, context.Transaction, MempoolValidator.StandardLocktimeVerifyFlags))
            {
                this.logger.LogTrace("(-)[FAIL_NONSTANDARD]");
                context.State.Fail(MempoolErrors.NonFinal).Throw();
            }
        }

        /// <summary>
        /// Validate the transaction is a standard transaction.
        /// Checks the version number, transaction size, input signature size,
        /// output script template, single output, & dust outputs.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/aa624b61c928295c27ffbb4d27be582f5aa31b56/src/policy/policy.cpp##L82-L144"/>
        /// </summary>
        /// <param name="context">Current validation context.</param>
        /// <param name="witnessEnabled">Whether witness is enabled.</param>
        private void CheckStandardTransaction(MempoolValidationContext context, bool witnessEnabled)
        {
            // TODO: Implement Witness Code

            Transaction tx = context.Transaction;
            if (tx.Version > this.ConsensusOptions.MaxStandardVersion || tx.Version < 1)
            {
                this.logger.LogTrace("(-)[FAIL_TX_VERSION]");
                context.State.Fail(MempoolErrors.Version).Throw();
            }

            if (this.network.Consensus.IsProofOfStake)
            {
                long adjustedTime = this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
                PosFutureDriftRule futureDriftRule = this.consensusRules.GetRule<PosFutureDriftRule>();

                // nTime has different purpose from nLockTime but can be used in similar attacks
                if (tx.Time > adjustedTime + futureDriftRule.GetFutureDrift(adjustedTime))
                {
                    context.State.Fail(MempoolErrors.TimeTooNew).Throw();
                }
            }

            // Extremely large transactions with lots of inputs can cost the network
            // almost as much to process as they cost the sender in fees, because
            // computing signature hashes is O(ninputs*txsize). Limiting transactions
            // to MAX_STANDARD_TX_WEIGHT mitigates CPU exhaustion attacks.
            int sz = GetTransactionWeight(tx, this.ConsensusOptions);
            if (sz >= this.ConsensusOptions.MaxStandardTxWeight)
            {
                this.logger.LogTrace("(-)[FAIL_TX_SIZE]");
                context.State.Fail(MempoolErrors.TxSize).Throw();
            }

            foreach (TxIn txin in tx.Inputs)
            {
                // Biggest 'standard' txin is a 15-of-15 P2SH multisig with compressed
                // keys (remember the 520 byte limit on redeemScript size). That works
                // out to a (15*(33+1))+3=513 byte redeemScript, 513+1+15*(73+1)+3=1627
                // bytes of scriptSig, which we round off to 1650 bytes for some minor
                // future-proofing. That's also enough to spend a 20-of-20
                // CHECKMULTISIG scriptPubKey, though such a scriptPubKey is not
                // considered standard.
                if (txin.ScriptSig.Length > 1650)
                {
                    this.logger.LogTrace("(-)[FAIL_SCRIPTSIGSZ]");
                    context.State.Fail(MempoolErrors.ScriptsigSize).Throw();
                }

                if (!txin.ScriptSig.IsPushOnly)
                {
                    this.logger.LogTrace("(-)[FAIL_SCRIPTSIGPUSH]");
                    context.State.Fail(MempoolErrors.ScriptsigNotPushonly).Throw();
                }
            }

            int dataOut = 0;
            foreach (TxOut txout in tx.Outputs)
            {
                ScriptTemplate script = this.network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(txout.ScriptPubKey);
                if (script == null) //!::IsStandard(txout.scriptPubKey, whichType, witnessEnabled))  https://github.com/bitcoin/bitcoin/blob/aa624b61c928295c27ffbb4d27be582f5aa31b56/src/policy/policy.cpp#L57-L80
                {
                    this.logger.LogTrace("(-)[FAIL_SCRIPT_PUBKEY]");
                    context.State.Fail(MempoolErrors.Scriptpubkey).Throw();
                }

                if (script.Type == TxOutType.TX_NULL_DATA)
                    dataOut++;
                // TODO: fIsBareMultisigStd
                //else if ((script == PayToMultiSigTemplate.Instance))  (!fIsBareMultisigStd))
                //{
                //  context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "bare-multisig")).Throw();
                //}
                else if (txout.IsDust(this.minRelayTxFee))
                {
                    this.logger.LogTrace("(-)[FAIL_DUST]");
                    context.State.Fail(MempoolErrors.Dust).Throw();
                }
            }

            // only one OP_RETURN txout is permitted
            if (dataOut > 1)
            {
                this.logger.LogTrace("(-)[FAIL_MULTI_OPRETURN]");
                context.State.Fail(MempoolErrors.MultiOpReturn).Throw();
            }
        }

        /// <summary>
        /// Validates the transaction with the coin view.
        /// Checks if already in coin view, and missing and unavailable inputs.
        /// </summary>
        /// <param name="context">Validation context.</param>
        private void CheckMempoolCoinView(MempoolValidationContext context)
        {
            Guard.Assert(context.View != null);

            context.LockPoints = new LockPoints();

            // do we already have it?
            if (context.View.HaveCoins(context.TransactionHash))
            {
                this.logger.LogTrace("(-)[INVALID_ALREADY_KNOWN]");
                context.State.Invalid(MempoolErrors.AlreadyKnown).Throw();
            }

            // do all inputs exist?
            // Note that this does not check for the presence of actual outputs (see the next check for that),
            // and only helps with filling in pfMissingInputs (to determine missing vs spent).
            foreach (TxIn txin in context.Transaction.Inputs)
            {
                if (!context.View.HaveCoins(txin.PrevOut.Hash))
                {
                    context.State.MissingInputs = true;
                    this.logger.LogTrace("(-)[FAIL_MISSING_INPUTS]");
                    context.State.Fail(MempoolErrors.MissingInputs).Throw(); // fMissingInputs and !state.IsInvalid() is used to detect this condition, don't set state.Invalid()
                }
            }

            // are the actual inputs available?
            if (!context.View.HaveInputs(context.Transaction))
            {
                this.logger.LogTrace("(-)[INVALID_BAD_INPUTS]");
                context.State.Invalid(MempoolErrors.BadInputsSpent).Throw();
            }
        }

        /// <summary>
        /// Validates the transaction fee is valid.
        /// Checks whether the fee meets minimum fee, free transactions have sufficient priority,
        /// and absurdly high fees.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        public virtual void CheckFee(MempoolValidationContext context)
        {
            Money mempoolRejectFee = this.memPool.GetMinFee(this.mempoolSettings.MaxMempool * 1000000).GetFee(context.EntrySize);
            if (mempoolRejectFee > 0 && context.ModifiedFees < mempoolRejectFee)
            {
                this.logger.LogTrace("(-)[FAIL_MIN_FEE_NOT_MET]");
                context.State.Fail(MempoolErrors.MinFeeNotMet, $" {context.Fees} < {mempoolRejectFee}").Throw();
            }
            else if (this.mempoolSettings.RelayPriority && context.ModifiedFees < this.minRelayTxFee.GetFee(context.EntrySize) &&
                     !TxMempool.AllowFree(context.Entry.GetPriority(this.chain.Height + 1)))
            {
                this.logger.LogTrace("(-)[FAIL_INSUFFICENT_PRIORITY]");
                // Require that free transactions have sufficient priority to be mined in the next block.
                context.State.Fail(MempoolErrors.InsufficientPriority).Throw();
            }

            if (context.State.AbsurdFee > 0 && context.Fees > context.State.AbsurdFee)
            {
                this.logger.LogTrace("(-)[INVALID_ABSURD_FEE]");
                context.State.Invalid(MempoolErrors.AbsurdlyHighFee, $"{context.Fees} > {context.State.AbsurdFee}").Throw();
            }
        }

        /// <summary>
        /// Check that the transaction doesn't have an excessive number of sigops.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        private void CheckSigOps(MempoolValidationContext context)
        {
            // Check that the transaction doesn't have an excessive number of
            // sigops, making it impossible to mine. Since the coinbase transaction
            // itself can contain sigops MAX_STANDARD_TX_SIGOPS is less than
            // MAX_BLOCK_SIGOPS; we still consider this an invalid rather than
            // merely non-standard transaction.
            if (context.SigOpsCost > this.ConsensusOptions.MaxStandardTxSigopsCost)
            {
                this.logger.LogTrace("(-)[FAIL_TOO_MANY_SIGOPS]");
                context.State.Fail(MempoolErrors.TooManySigops).Throw();
            }
        }

        /// <summary>
        /// Creates a memory pool entry in the validation context.
        /// Validates the transactions can be mined, and the pay to script hashs are standard.
        /// Calculates the fees related to the transaction.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        /// <param name="acceptTime">The accept time to use for the entry.</param>
        private void CreateMempoolEntry(MempoolValidationContext context, long acceptTime)
        {
            // Only accept BIP68 sequence locked transactions that can be mined in the next
            // block; we don't want our mempool filled up with transactions that can't
            // be mined yet.
            // Must keep pool.cs for this unless we change CheckSequenceLocks to take a
            // CoinsViewCache instead of create its own
            if (!CheckSequenceLocks(this.network, this.chain.Tip, context, StandardLocktimeVerifyFlags, context.LockPoints))
            {
                this.logger.LogTrace("(-)[FAIL_BIP68_SEQLOCK]");
                context.State.Fail(MempoolErrors.NonBIP68Final).Throw();
            }

            // Check for non-standard pay-to-script-hash in inputs
            if (this.mempoolSettings.RequireStandard && !this.AreInputsStandard(context.Transaction, context.View))
            {
                this.logger.LogTrace("(-)[INVALID_NONSTANDARD_INPUTS]");
                context.State.Invalid(MempoolErrors.NonstandardInputs).Throw();
            }

            // Check for non-standard witness in P2WSH
            if (context.Transaction.HasWitness && this.mempoolSettings.RequireStandard && !this.IsWitnessStandard(context.Transaction, context.View))
            {
                this.logger.LogTrace("(-)[INVALID_NONSTANDARD_WITNESS]");
                context.State.Invalid(MempoolErrors.NonstandardWitness).Throw();
            }

            context.SigOpsCost = this.consensusRules.GetRule<CoinViewRule>().GetTransactionSignatureOperationCost(context.Transaction, context.View.Set,
                new DeploymentFlags { ScriptFlags = ScriptVerify.Standard });

            Money nValueIn = context.View.GetValueIn(context.Transaction);

            context.ValueOut = context.Transaction.TotalOut;
            context.Fees = nValueIn - context.ValueOut;
            // nModifiedFees includes any fee deltas from PrioritiseTransaction
            Money nModifiedFees = context.Fees;
            double priorityDummy = 0;
            this.memPool.ApplyDeltas(context.TransactionHash, ref priorityDummy, ref nModifiedFees);
            context.ModifiedFees = nModifiedFees;

            (double dPriority, Money inChainInputValue) = context.View.GetPriority(context.Transaction, this.chain.Height);

            // Keep track of transactions that spend a coinbase, which we re-scan
            // during reorgs to ensure COINBASE_MATURITY is still met.
            bool spendsCoinbase = context.View.SpendsCoinBase(context.Transaction);

            context.Entry = new TxMempoolEntry(context.Transaction, context.Fees, acceptTime, dPriority, this.chain.Height, inChainInputValue,
                spendsCoinbase, context.SigOpsCost, context.LockPoints, this.ConsensusOptions);
            context.EntrySize = (int)context.Entry.GetTxSize();
        }

        /// <summary>
        /// Check if transaction can replace others.
        /// Only transactions that increase fees over previous transactions are accepted.
        /// There is a restriction on the maximum number of transactions that would be replaced.
        /// The new transaction must have all inputs confirmed.
        /// The new transaction must have sufficient fees to pay for it's bandwidth.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        private void CheckReplacment(MempoolValidationContext context)
        {
            // Check if it's economically rational to mine this transaction rather
            // than the ones it replaces.
            context.ConflictingFees = 0;
            context.ConflictingSize = 0;
            context.ConflictingCount = 0;
            context.AllConflicting = new TxMempool.SetEntries();

            // If we don't hold the lock allConflicting might be incomplete; the
            // subsequent RemoveStaged() and addUnchecked() calls don't guarantee
            // mempool consistency for us.
            //LOCK(pool.cs);
            if (context.SetConflicts.Any())
            {
                var newFeeRate = new FeeRate(context.ModifiedFees, context.EntrySize);
                var setConflictsParents = new List<uint256>();
                const int MaxDescendantsToVisit = 100;
                var setIterConflicting = new TxMempool.SetEntries();
                foreach (uint256 hashConflicting in context.SetConflicts)
                {
                    TxMempoolEntry mi = this.memPool.MapTx.TryGet(hashConflicting);
                    if (mi == null)
                        continue;

                    // Save these to avoid repeated lookups
                    setIterConflicting.Add(mi);

                    // Don't allow the replacement to reduce the feerate of the
                    // mempool.
                    //
                    // We usually don't want to accept replacements with lower
                    // feerates than what they replaced as that would lower the
                    // feerate of the next block. Requiring that the feerate always
                    // be increased is also an easy-to-reason about way to prevent
                    // DoS attacks via replacements.
                    //
                    // The mining code doesn't (currently) take children into
                    // account (CPFP) so we only consider the feerates of
                    // transactions being directly replaced, not their indirect
                    // descendants. While that does mean high feerate children are
                    // ignored when deciding whether or not to replace, we do
                    // require the replacement to pay more overall fees too,
                    // mitigating most cases.
                    var oldFeeRate = new FeeRate(mi.ModifiedFee, (int)mi.GetTxSize());
                    if (newFeeRate <= oldFeeRate)
                    {
                        this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_FEE]");
                        context.State.Fail(MempoolErrors.InsufficientFee,
                            $"rejecting replacement {context.TransactionHash}; new feerate {newFeeRate} <= old feerate {oldFeeRate}").Throw();
                    }

                    foreach (TxIn txin in mi.Transaction.Inputs)
                    {
                        setConflictsParents.Add(txin.PrevOut.Hash);
                    }

                    context.ConflictingCount += mi.CountWithDescendants;
                }
                // This potentially overestimates the number of actual descendants
                // but we just want to be conservative to avoid doing too much
                // work.
                if (context.ConflictingCount <= MaxDescendantsToVisit)
                {
                    // If not too many to replace, then calculate the set of
                    // transactions that would have to be evicted
                    foreach (TxMempoolEntry it in setIterConflicting)
                    {
                        this.memPool.CalculateDescendants(it, context.AllConflicting);
                    }
                    foreach (TxMempoolEntry it in context.AllConflicting)
                    {
                        context.ConflictingFees += it.ModifiedFee;
                        context.ConflictingSize += it.GetTxSize();
                    }
                }
                else
                {
                    this.logger.LogTrace("(-)[FAIL_TOO_MANY_POTENTIAL_REPLACEMENTS]");
                    context.State.Fail(MempoolErrors.TooManyPotentialReplacements,
                            $"rejecting replacement {context.TransactionHash}; too many potential replacements ({context.ConflictingCount} > {MaxDescendantsToVisit})").Throw();
                }

                for (int j = 0; j < context.Transaction.Inputs.Count; j++)
                {
                    // We don't want to accept replacements that require low
                    // feerate junk to be mined first. Ideally we'd keep track of
                    // the ancestor feerates and make the decision based on that,
                    // but for now requiring all new inputs to be confirmed works.
                    if (!setConflictsParents.Contains(context.Transaction.Inputs[j].PrevOut.Hash))
                    {
                        // Rather than check the UTXO set - potentially expensive -
                        // it's cheaper to just check if the new input refers to a
                        // tx that's in the mempool.
                        if (this.memPool.MapTx.ContainsKey(context.Transaction.Inputs[j].PrevOut.Hash))
                        {
                            this.logger.LogTrace("(-)[FAIL_REPLACEMENT_ADDS_UNCONFIRMED]");
                            context.State.Fail(MempoolErrors.ReplacementAddsUnconfirmed,
                                $"replacement {context.TransactionHash} adds unconfirmed input, idx {j}").Throw();
                        }
                    }
                }

                // The replacement must pay greater fees than the transactions it
                // replaces - if we did the bandwidth used by those conflicting
                // transactions would not be paid for.
                if (context.ModifiedFees < context.ConflictingFees)
                {
                    this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_FEE]");
                    context.State.Fail(MempoolErrors.Insufficientfee,
                            $"rejecting replacement {context.TransactionHash}, less fees than conflicting txs; {context.ModifiedFees} < {context.ConflictingFees}").Throw();
                }

                // Finally in addition to paying more fees than the conflicts the
                // new transaction must pay for its own bandwidth.
                Money nDeltaFees = context.ModifiedFees - context.ConflictingFees;
                if (nDeltaFees < this.minRelayTxFee.GetFee(context.EntrySize))
                {
                    this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_DELTA_FEE]");
                    context.State.Fail(MempoolErrors.Insufficientfee,
                            $"rejecting replacement {context.TransactionHash}, not enough additional fees to relay; {nDeltaFees} < {this.minRelayTxFee.GetFee(context.EntrySize)}").Throw();
                }
            }
        }

        /// <summary>
        /// Validates the rate limit.
        /// Currently not implemented.
        /// </summary>
        /// <param name="context">Current validation context</param>
        /// <param name="limitFree">Whether to limit free transactioins</param>
        private void CheckRateLimit(MempoolValidationContext context, bool limitFree)
        {
            // TODO: sort this logic
            return;
        }

        /// <summary>
        /// Validates the ancestors of a memory pool entry.
        /// Checks that the number of ancestors isn't too large.
        /// Checks for a transaction that spends outputs that would be replaced by it.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        private void CheckAncestors(MempoolValidationContext context)
        {
            // Calculate in-mempool ancestors, up to a limit.
            context.SetAncestors = new TxMempool.SetEntries();
            int nLimitAncestors = this.mempoolSettings.LimitAncestors;
            int nLimitAncestorSize = this.mempoolSettings.LimitAncestorSize * 1000;
            int nLimitDescendants = this.mempoolSettings.LimitDescendants;
            int nLimitDescendantSize = this.mempoolSettings.LimitDescendantSize * 1000;
            string errString;
            if (!this.memPool.CalculateMemPoolAncestors(context.Entry, context.SetAncestors, nLimitAncestors,
                nLimitAncestorSize, nLimitDescendants, nLimitDescendantSize, out errString))
            {
                this.logger.LogTrace("(-)FAIL_CHAIN_TOO_LONG]");
                context.State.Fail(MempoolErrors.TooLongMempoolChain, errString).Throw();
            }

            // A transaction that spends outputs that would be replaced by it is invalid. Now
            // that we have the set of all ancestors we can detect this
            // pathological case by making sure setConflicts and setAncestors don't
            // intersect.
            foreach (TxMempoolEntry ancestorIt in context.SetAncestors)
            {
                uint256 hashAncestor = ancestorIt.TransactionHash;
                if (context.SetConflicts.Contains(hashAncestor))
                {
                    this.logger.LogTrace("(-)[FAIL_BAD_TX_SPENDS_CONFLICTING]");
                    context.State.Fail(MempoolErrors.BadTxnsSpendsConflictingTx,
                        $"{context.TransactionHash} spends conflicting transaction {hashAncestor}").Throw();
                }
            }
        }

        /// <summary>
        /// Trims memory pool to a new size.
        /// First expires transactions older than age.
        /// Then trims memory pool to limit if necessary.
        /// </summary>
        /// <param name="limit">New size.</param>
        /// <param name="age">AAge to use for calculating expired transactions.</param>
        private void LimitMempoolSize(long limit, long age)
        {
            int expired = this.memPool.Expire(this.dateTimeProvider.GetTime() - age);
            if (expired != 0)
                this.logger.LogInformation($"Expired {expired} transactions from the memory pool");

            var vNoSpendsRemaining = new List<uint256>();
            this.memPool.TrimToSize(limit, vNoSpendsRemaining);
        }

        /// <summary>
        /// Whether chain is currently valid for fee estimation.
        /// It should only count for fee estimation if the node is not behind.
        /// </summary>
        /// <returns>Whether current for fee estimation.</returns>
        private bool IsCurrentForFeeEstimation()
        {
            // TODO: implement method (find a way to know if in IBD)

            //if (IsInitialBlockDownload())
            //  return false;

            if (this.chain.Tip.Header.BlockTime.ToUnixTimeMilliseconds() < (this.dateTimeProvider.GetTime() - MaxFeeEstimationTipAge))
            {
                return false;
            }

            //if (chainActive.Height() < pindexBestHeader->nHeight - 1)
            //  return false;

            return true;
        }

        /// <summary>
        /// Validate inputs against previous transactions.
        /// Checks against <see cref="ScriptVerify.Standard"/> and <see cref="ScriptVerify.P2SH"/>
        /// </summary>
        /// <param name="context">Current validation context.</param>
        private void CheckAllInputs(MempoolValidationContext context)
        {
            var scriptVerifyFlags = ScriptVerify.Standard;
            if (!this.mempoolSettings.RequireStandard)
            {
                // TODO: implement -promiscuousmempoolflags
                // scriptVerifyFlags = GetArg("-promiscuousmempoolflags", scriptVerifyFlags);
            }

            // Check against previous transactions
            // This is done last to help prevent CPU exhaustion denial-of-service attacks.
            var txdata = new PrecomputedTransactionData(context.Transaction);
            if (!this.CheckInputs(context, scriptVerifyFlags, txdata))
            {
                // TODO: Implement Witness Code
                //// SCRIPT_VERIFY_CLEANSTACK requires SCRIPT_VERIFY_WITNESS, so we
                //// need to turn both off, and compare against just turning off CLEANSTACK
                //// to see if the failure is specifically due to witness validation.
                //if (!tx.HasWitness() && CheckInputs(Trx, state, view, true, scriptVerifyFlags & ~(SCRIPT_VERIFY_WITNESS | SCRIPT_VERIFY_CLEANSTACK), true, txdata) &&
                //  !CheckInputs(tx, state, view, true, scriptVerifyFlags & ~SCRIPT_VERIFY_CLEANSTACK, true, txdata))
                //{
                //  // Only the witness is missing, so the transaction itself may be fine.
                //  state.SetCorruptionPossible();
                //}

                this.logger.LogTrace("(-)[FAIL_INPUTS_PREV_TXS]");
                context.State.Fail(new MempoolError()).Throw();
            }

            // Check again against just the consensus-critical mandatory script
            // verification flags, in case of bugs in the standard flags that cause
            // transactions to pass as valid when they're actually invalid. For
            // instance the STRICTENC flag was incorrectly allowing certain
            // CHECKSIG NOT scripts to pass, even though they were invalid.
            //
            // There is a similar check in CreateNewBlock() to prevent creating
            // invalid blocks, however allowing such transactions into the mempool
            // can be exploited as a DoS attack.
            if (!this.CheckInputs(context, ScriptVerify.P2SH, txdata))
            {
                this.logger.LogTrace("(-)[FAIL_SCRIPT_VERIFY]");
                context.State.Fail(new MempoolError(),
                        $"CheckInputs: BUG! PLEASE REPORT THIS! ConnectInputs failed against MANDATORY but not STANDARD flags {context.TransactionHash}").Throw();
            }
        }

        /// <summary>
        /// Validates transaction inputs against transaction data for a specific script verify flag.
        /// Check whether all inputs of this transaction are valid (no double spends, scripts & sigs, amounts)
        /// This does not modify the UTXO set. If pvChecks is not NULL, script checks are pushed onto it
        /// instead of being performed inline.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        /// <param name="scriptVerify">Script verify flag.</param>
        /// <param name="txData">Transaction data.</param>
        /// <returns>Whether inputs are valid.</returns>
        private bool CheckInputs(MempoolValidationContext context, ScriptVerify scriptVerify,
            PrecomputedTransactionData txData)
        {
            Transaction tx = context.Transaction;
            if (!context.Transaction.IsCoinBase)
            {
                this.consensusRules.GetRule<CoinViewRule>().CheckInputs(context.Transaction, context.View.Set, this.chain.Height + 1);

                for (int iInput = 0; iInput < tx.Inputs.Count; iInput++)
                {
                    TxIn input = tx.Inputs[iInput];
                    int iiIntput = iInput;
                    TxOut txout = context.View.GetOutputFor(input);

                    var checker = new TransactionChecker(tx, iiIntput, txout.Value, txData);
                    var ctx = new ScriptEvaluationContext(this.network);
                    ctx.ScriptVerify = scriptVerify;
                    if (ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker))
                    {
                        this.logger.LogTrace("(-)[SCRIPT_VERIFIED]:true");
                        return true;
                    }
                    else
                    {
                        //TODO:

                        //if (flags & STANDARD_NOT_MANDATORY_VERIFY_FLAGS)
                        //{
                        //  // Check whether the failure was caused by a
                        //  // non-mandatory script verification check, such as
                        //  // non-standard DER encodings or non-null dummy
                        //  // arguments; if so, don't trigger DoS protection to
                        //  // avoid splitting the network between upgraded and
                        //  // non-upgraded nodes.
                        //  CScriptCheck check2(*coins, tx, i,
                        //          flags & ~STANDARD_NOT_MANDATORY_VERIFY_FLAGS, cacheStore, &txdata);
                        //  if (check2())
                        //      return state.Invalid(false, REJECT_NONSTANDARD, strprintf("non-mandatory-script-verify-flag (%s)", ScriptErrorString(check.GetScriptError())));
                        //}
                        //// Failures of other flags indicate a transaction that is
                        //// invalid in new blocks, e.g. a invalid P2SH. We DoS ban
                        //// such nodes as they are not following the protocol. That
                        //// said during an upgrade careful thought should be taken
                        //// as to the correct behavior - we may want to continue
                        //// peering with non-upgraded nodes even after soft-fork
                        //// super-majority signaling has occurred.
                        this.logger.LogTrace("(-)[FAIL_SCRIPT_VERIFY]");
                        context.State.Fail(MempoolErrors.MandatoryScriptVerifyFlagFailed, ctx.Error.ToString()).Throw();
                    }

                }
            }

            return true;
        }

        /// <summary>
        /// Whether transaction inputs are standard.
        /// Check for standard transaction types.
        /// </summary>
        /// <param name="tx">Transaction to verify.</param>
        /// <param name="mapInputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Whether all inputs (scriptSigs) use only standard transaction forms.</returns>
        private bool AreInputsStandard(Transaction tx, MempoolCoinView mapInputs)
        {
            if (tx.IsCoinBase)
            {
                this.logger.LogTrace("(-)[IS_COINBASE]:true");
                return true; // Coinbases don't use vin normally
            }

            foreach (TxIn txin in tx.Inputs)
            {
                TxOut prev = mapInputs.GetOutputFor(txin);
                ScriptTemplate template = this.network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(prev.ScriptPubKey);
                if (template == null)
                {
                    this.logger.LogTrace("(-)[BAD_SCRIPT_TEMPLATE]:false");
                    return false;
                }

                if (template.Type == TxOutType.TX_SCRIPTHASH)
                {
                    if (prev.ScriptPubKey.GetSigOpCount(true) > 15) //MAX_P2SH_SIGOPS
                    {
                        this.logger.LogTrace("(-)[SIG_OP_MAX]:false");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether transaction is witness standard.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/aa624b61c928295c27ffbb4d27be582f5aa31b56/src/policy/policy.cpp#L196"/>
        /// </summary>
        /// <param name="tx">Transaction to verify.</param>
        /// <param name="mapInputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Whether transaction is witness standard.</returns>
        private bool IsWitnessStandard(Transaction tx, MempoolCoinView mapInputs)
        {
            if (tx.IsCoinBase)
            {
                this.logger.LogTrace("(-)[IS_COINBASE]:true");
                return true; // Coinbases are skipped.
            }

            foreach (TxIn input in tx.Inputs)
            {
                // We don't care if witness for this input is empty, since it must not be bloated.
                // If the script is invalid without witness, it would be caught sooner or later during validation.
                if (input.WitScript == null)
                    continue;

                TxOut prev = mapInputs.GetOutputFor(input);

                // Get the scriptPubKey corresponding to this input.
                Script prevScript = prev.ScriptPubKey;
                if (prevScript.IsPayToScriptHash(this.network))
                {
                    // If the scriptPubKey is P2SH, we try to extract the redeemScript casually by converting the scriptSig
                    // into a stack. We do not check IsPushOnly nor compare the hash as these will be done later anyway.
                    // If the check fails at this stage, we know that this txid must be a bad one.
                    PayToScriptHashSigParameters sigParams = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(this.network, input.ScriptSig);
                    if (sigParams == null || sigParams.RedeemScript == null)
                    {
                        this.logger.LogTrace("(-)[BAD_TXID]:false");
                        return false;
                    }

                    prevScript = sigParams.RedeemScript;
                }

                // Non-witness program must not be associated with any witness.
                if (!prevScript.IsWitness(this.network))
                {
                    this.logger.LogTrace("(-)[WITNESS_MISMATCH]:false");
                    return false;
                }

                // Check P2WSH standard limits.
                WitProgramParameters wit = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(this.chain.Network, prevScript);
                if (wit == null)
                {
                    this.logger.LogTrace("(-)[BAD_WITNESS_PARAMS]:false");
                    return false;
                }

                // Version 0 segregated witness program validation.
                if (wit.Version == 0 && wit.Program.Length == 32)
                {
                    const int MaxStandardP2wshScriptSize = 3600;
                    const int MaxStandardP2wshStackItems = 100;
                    const int MaxStandardP2wshStackItemSize = 80;

                    WitScript witness = input.WitScript;

                    // Get P2WSH script from top of stack.
                    Script scriptPubKey = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));

                    // Stack items are remainder of stack.
                    int sizeWitnessStack = witness.PushCount - 1;

                    // Get the witness stack items.
                    var stack = new List<byte[]>();
                    for (int i = 0; i < sizeWitnessStack; i++)
                    {
                        stack.Add(witness.GetUnsafePush(i));
                    }

                    // Validate P2WSH script isn't larger than max length.
                    if (scriptPubKey.ToBytes(true).Length > MaxStandardP2wshScriptSize)
                    {
                        this.logger.LogTrace("(-)[P2WSH_SCRIPT_SIZE]:false");
                        return false;
                    }

                    // Validate number items in witness stack isn't larger than max.
                    if (sizeWitnessStack > MaxStandardP2wshStackItems)
                    {
                        this.logger.LogTrace("(-)[P2WSH_STACK_ITEMS]:false");
                        return false;
                    }

                    // Validate size of each of the witness stack items.
                    for (int j = 0; j < sizeWitnessStack; j++)
                    {
                        if (stack[j].Length > MaxStandardP2wshStackItemSize)
                        {
                            this.logger.LogTrace("(-)[P2WSH_STACK_ITEM_SIZE]:false");
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
