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
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
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
        private readonly ChainIndexer chainIndexer;

        /// <summary>Coin view of the memory pool.</summary>
        private readonly ICoinView coinView;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        private readonly ITxMempool memPool;

        /// <summary>Instance logger for memory pool validator.</summary>
        private readonly ILogger logger;

        /// <summary>Minimum fee rate for a relay transaction.</summary>
        private readonly FeeRate minRelayTxFee;

        /// <summary>Flags that determine how transaction should be validated in non-consensus code.</summary>
        public static Transaction.LockTimeFlags StandardLocktimeVerifyFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

        private readonly IConsensusRuleEngine consensusRules;

        private readonly NodeDeployments nodeDeployments;

        // TODO: Implement Later with CheckRateLimit()
        //private readonly FreeLimiterSection freeLimiter;

        //private class FreeLimiterSection
        //{
        //  public double FreeCount;
        //  public long LastTime;
        //}

        private Network network;

        private readonly List<IMempoolRule> mempoolRules;

        public MempoolValidator(
            ITxMempool memPool,
            MempoolSchedulerLock mempoolLock,
            IDateTimeProvider dateTimeProvider,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ICoinView coinView,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IConsensusRuleEngine consensusRules,
            IEnumerable<IMempoolRule> mempoolRules,
            NodeDeployments nodeDeployments)
        {
            this.memPool = memPool;
            this.mempoolLock = mempoolLock;
            this.dateTimeProvider = dateTimeProvider;
            this.mempoolSettings = mempoolSettings;
            this.chainIndexer = chainIndexer;
            this.network = chainIndexer.Network;
            this.coinView = coinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            // TODO: Implement later with CheckRateLimit()
            // this.freeLimiter = new FreeLimiterSection();
            this.PerformanceCounter = new MempoolPerformanceCounter(this.dateTimeProvider);
            this.minRelayTxFee = nodeSettings.MinRelayTxFeeRate;
            this.consensusRules = consensusRules;
            this.nodeDeployments = nodeDeployments;
            this.mempoolRules = mempoolRules.ToList();
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

                if (state.IsInvalid)
                {
                    this.logger.LogTrace("(-):false");
                    return false;
                }

                this.logger.LogTrace("(-):true");
                return true;
            }
            catch (MempoolErrorException mempoolError)
            {
                this.logger.LogDebug("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(MempoolErrorException), mempoolError.Message, mempoolError.ValidationState?.Error?.Code, mempoolError.ValidationState?.ErrorMessage);
                this.logger.LogTrace("(-)[MEMPOOL_EXCEPTION]:false");
                return false;
            }
            catch (ConsensusErrorException consensusError)
            {
                this.logger.LogDebug("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(ConsensusErrorException), consensusError.Message, consensusError.ConsensusError?.Code, consensusError.ConsensusError?.Message);
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
        /// <param name="chainIndexer">Block chain used for computing time-locking on the transaction.</param>
        /// <param name="dateTimeProvider">Provides the current date and time.</param>
        /// <param name="tx">The transaction to validate.</param>
        /// <param name="flags">Flags for time-locking the transaction.</param>
        /// <returns>Whether the final transaction was valid.</returns>
        /// <seealso cref="Transaction.IsFinal(DateTimeOffset, int)"/>
        public static bool CheckFinalTransaction(ChainIndexer chainIndexer, IDateTimeProvider dateTimeProvider, Transaction tx, Transaction.LockTimeFlags flags)
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
            int blockHeight = chainIndexer.Height + 1;

            // BIP113 will require that time-locked transactions have nLockTime set to
            // less than the median time of the previous block they're contained in.
            // When the next block is created its previous block will be the current
            // chain tip, so we use that to calculate the median time passed to
            // IsFinalTx() if LOCKTIME_MEDIAN_TIME_PAST is set.
            DateTimeOffset blockTime = flags.HasFlag(StandardLocktimeVerifyFlags)
                ? chainIndexer.Tip.Header.BlockTime
                : DateTimeOffset.FromUnixTimeMilliseconds(dateTimeProvider.GetTime());

            return tx.IsFinal(blockTime, blockHeight);
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

            context.MinRelayTxFee = this.minRelayTxFee;

            // TODO: Convert these into rules too
            this.PreMempoolChecks(context);

            // create the MemPoolCoinView and load relevant utxoset
            context.View = new MempoolCoinView(this.coinView, this.memPool, this.mempoolLock, this);

            // adding to the mem pool can only be done sequentially
            // use the sequential scheduler for that.
            await this.mempoolLock.WriteAsync(() =>
            {
                context.View.LoadViewLocked(context.Transaction);

                // If the transaction already exists in the mempool,
                // we only record the state but do not throw an exception.
                // This is because the caller will check if the state is invalid
                // and if so return false, meaning that the transaction should not be relayed.
                if (this.memPool.Exists(context.TransactionHash))
                {
                    state.Invalid(MempoolErrors.InPool);
                    this.logger.LogTrace("(-)[INVALID_TX_ALREADY_EXISTS]");
                    return;
                }

                foreach (IMempoolRule rule in this.mempoolRules)
                {
                    rule.CheckTransaction(context);
                }

                // Remove conflicting transactions from the mempool
                foreach (TxMempoolEntry it in context.AllConflicting)
                    this.logger.LogInformation($"Replacing tx {it.TransactionHash} with {context.TransactionHash} for {context.ModifiedFees - context.ConflictingFees} BTC additional fees, {context.EntrySize - context.ConflictingSize} delta bytes");

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
        /// Checks that are done before touching the memory pool.
        /// These checks don't need to run under the memory pool lock.
        /// </summary>
        /// <param name="context">Current validation context.</param>
        protected virtual void PreMempoolChecks(MempoolValidationContext context)
        {
            // TODO: fix this to use dedicated mempool rules.
            new CheckPowTransactionRule { Logger = this.logger }.CheckTransaction(this.network, this.network.Consensus.Options, context.Transaction);
            if (this.chainIndexer.Network.Consensus.IsProofOfStake)
                new CheckPosTransactionRule { Logger = this.logger }.CheckTransaction(context.Transaction);

            // Coinbase is only valid in a block, not as a loose transaction
            if (context.Transaction.IsCoinBase)
            {
                this.logger.LogTrace("(-)[FAIL_INVALID_COINBASE]");
                context.State.Fail(MempoolErrors.Coinbase).Throw();
            }

            // Coinstake is only valid in a block, not as a loose transaction
            // TODO: mempool needs to have separate checks for POW/POS as part of the change to rules.
            if (this.network.Consensus.IsProofOfStake && context.Transaction.IsCoinStake)
            {
                this.logger.LogTrace("(-)[FAIL_INVALID_COINSTAKE]");
                context.State.Fail(MempoolErrors.Coinstake).Throw();
            }

            bool witnessEnabled = false;

            // Rather not work on nonstandard transactions (unless -testnet/-regtest)
            if (this.mempoolSettings.RequireStandard)
            {
                this.CheckStandardTransaction(context);
            }

            // Only accept nLockTime-using transactions that can be mined in the next
            // block; we don't want our mempool filled up with transactions that can't
            // be mined yet.
            if (!CheckFinalTransaction(this.chainIndexer, this.dateTimeProvider, context.Transaction, MempoolValidator.StandardLocktimeVerifyFlags))
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
        private void CheckStandardTransaction(MempoolValidationContext context)
        {
            Transaction tx = context.Transaction;
            if (tx.Version > this.network.Consensus.Options.MaxStandardVersion || tx.Version < 1)
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
            int sz = GetTransactionWeight(tx, this.network.Consensus.Options);
            if (sz >= this.network.Consensus.Options.MaxStandardTxWeight)
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
                {
                    dataOut++;
                }
                //else if ((script == PayToMultiSigTemplate.Instance) && !this.mempoolSettings.PermitBareMultisig)
                //{
                //    context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "bare-multisig")).Throw();
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

            if (this.chainIndexer.Tip.Header.BlockTime.ToUnixTimeMilliseconds() < (this.dateTimeProvider.GetTime() - MaxFeeEstimationTipAge))
            {
                return false;
            }

            //if (chainActive.Height() < pindexBestHeader->nHeight - 1)
            //  return false;

            return true;
        }
    }
}
