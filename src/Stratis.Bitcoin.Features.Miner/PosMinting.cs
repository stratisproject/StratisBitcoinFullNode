﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// <see cref="PosMinting"/> is used in order to generate new blocks. It involves a sort of lottery, similar to proof-of-work,
    /// but the chances of winning this lottery is proportional to how many coins you are staking, not on hashing power.
    /// </summary>
    public interface IPosMinting
    {
        /// <summary>
        /// Creates a coinstake transaction with kernel that satisfies POS staking target.
        /// </summary>
        /// <param name="utxoStakeDescriptions">List of UTXOs that are available in the wallet for staking.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="chainTip">Tip of the best chain.</param>
        /// <param name="searchInterval">Length of an unexplored block time space in seconds. It only makes sense to look for a solution within this interval.</param>
        /// <param name="fees">Transaction fees from the transactions included in the block if we mine it.</param>
        /// <param name="coinstakeContext">Information about coinstake transaction and its private key that is to be filled when the kernel is found.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        Task<bool> CreateCoinstakeAsync(List<PosMinting.UtxoStakeDescription> utxoStakeDescriptions, Block block, ChainedBlock chainTip, long searchInterval, long fees, PosMinting.CoinstakeContext coinstakeContext);

        /// <summary>
        /// Attempts to stake new blocks in a loop.
        /// <para>
        /// Staking is attempted only if the node is fully synchronized and connected to the network.
        /// </para>
        /// </summary>
        /// <param name="walletSecret">Credentials to the wallet with which will be used for staking.</param>
        Task GenerateBlocksAsync(PosMinting.WalletSecret walletSecret);

        /// <summary>
        /// Calculates staking difficulty for a specific block.
        /// </summary>
        /// <param name="block">Block at which to calculate the difficulty.</param>
        /// <returns>Staking difficulty.</returns>
        /// <remarks>
        /// The actual idea behind the calculation is a mystery. It was simply ported from
        /// <see cref="https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L16"/>.
        /// </remarks>
        double GetDifficulty(ChainedBlock block);

        /// <summary>
        /// Constructs model for RPC "getstakinginfo" call.
        /// </summary>
        /// <returns>Staking information RPC response.</returns>
        Models.GetStakingInfoModel GetGetStakingInfoModel();

        /// <summary>
        /// Calculates the total balance from all UTXOs in the wallet that are mature.
        /// </summary>
        /// <param name="utxoStakeDescriptions">Description of coins in the wallet that will be used for staking.</param>
        /// <returns>Total balance from all UTXOs in the wallet that are mature.</returns>
        Money GetMatureBalance(List<PosMinting.UtxoStakeDescription> utxoStakeDescriptions);

        /// <summary>
        /// Estimates the total staking weight of the network.
        /// </summary>
        /// <returns>Estimated amount of money that is used by all stakers on the network.</returns>
        /// <remarks>
        /// The idea behind estimating the network staking weight is very similar to estimating
        /// the total hash power of PoW network. The difficulty retarget algorithm tries to make
        /// sure of certain distribution of the blocks over a period of time. Base on real distribution
        /// and using the actual difficulty targets, one is able to compute how much stake was
        /// presented on the network to generate each block.
        /// <para>
        /// The method was ported from
        /// <see cref="https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L74"/>.
        /// </para>
        /// </remarks>
        double GetNetworkWeight();

        /// <summary>
        /// Starts the main POS staking loop.
        /// </summary>
        /// <param name="walletSecret">Credentials to the wallet with which will be used for staking.</param>
        /// <returns>Interface to started loop, so it can be stopped during shutdown.</returns>
        IAsyncLoop Stake(PosMinting.WalletSecret walletSecret);
    }

    /// <summary>
    /// <see cref="PosMinting"/> is used in order to generate new blocks. It involves a sort of lottery, similar to proof-of-work,
    /// but the chances of winning this lottery is proportional to how many coins you are staking, not on hashing power.
    /// </summary>
    /// <remarks>
    /// Staking is attempted only if the node is fully synchronized and connected to the network.
    /// If not it will wait till node is synced. Only transactions that were confirmed at least
    /// <see cref="PosConsensusOptions.StakeMinConfirmations"/> blocks ago are eligible for staking.
    /// <para>
    /// The overall process for "attempting" to mine a PoS block looks like this:
    /// <list type="number">
    /// <item>Create new block with transactions from mempool.</item>
    /// <item>Get UTXOs that can participate in staking (have suitable depth).</item>
    /// <item>Split these UTXOs in subsets and create tasks processing each subset to allow for parallel processing.</item>
    /// <item>Each of the tasks mentioned above will try to find a solution for proof of stake target. This is done by creating a coinstake
    /// transaction with each of the available UTXOs combined with all valid unix timestamps that were not checked.
    /// Those timestamps are within a time interval from now to now - searchInterval seconds. Only timestamps that are divisible by
    /// <c><see cref="PosConsensusValidator.StakeTimestampMask"/> + 1</c> are valid candidates (this is done to decrease granularity of timestamps).
    /// Search interval is a length of an unexplored block time space in seconds.
    /// Task calculates the kernel's hash (kernel is the first input in the coinstake transaction) using the next formula:
    /// <c>hash(stakeModifierV2 + stakingCoins.Time + prevout.Hash + prevout.N + transactionTime)</c>.
    /// Then it calculates staking target using the next formula: <c>block difficulty * UTXO value</c>.
    /// We compare kernel's hash against the staking target, if it's greater then we met the criteria and kernel is found.
    /// So the more coins we stake the higher the staking target and so the higher the chance to meet the criteria.</item>
    /// <item>In case kernel is found we add a coinstake transaction, sign the block and add it to the chain.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Coinstake transaction invalidates previous inputs and spends the inputs to new outputs with the additional stake reward.
    /// </para>
    /// <para>
    /// The purpose of stake modifier is to prevent a UTXO owner from computing future proof-of-stake
    /// generated by this UTXO at the time of transaction confirmation. As described above, the stake modifier
    /// is included in the hash that must meet the difficulty target. As the stake modifier changes with each block
    /// and the new value depends on the kernel, it is hard to predict its value in the future.
    /// </para>
    /// </remarks>
    public class PosMinting : IPosMinting
    {
        /// <summary>
        /// Information related to UTXO that is required for staking.
        /// </summary>
        public class UtxoStakeDescription
        {
            /// <summary>Block's hash.</summary>
            public uint256 HashBlock { get; set; }

            /// <summary>UTXO that participates in staking. It's a part of <see cref="UtxoSet"/>.</summary>
            public TxOut TxOut { get; set; }

            /// <summary>Information about transaction id and index.</summary>
            public OutPoint OutPoint { get; set; }

            /// <summary>Address of the transaction that has spendable coins for staking.</summary>
            public HdAddress Address { get; set; }

            /// <summary>Selected outputs of a transaction.</summary>
            public UnspentOutputs UtxoSet { get; set; }

            /// <summary>Credentials to wallet that contains the private key for the staking UTXO.</summary>
            public WalletSecret Secret { get; set; }

            /// <summary>Private key that is needed for spending coins associated with the <see cref="Address"/>.</summary>
            public Key Key { get; set; }
        }

        /// <summary>
        /// Credentials to wallet that contains the private key for the staking UTXO.
        /// </summary>
        public class WalletSecret
        {
            /// <summary>Wallet's password that is needed for getting wallet's private key which is used for signing generated blocks.</summary>
            public string WalletPassword { get; set; }

            /// <summary>Name of the wallet which UTXOs are used for staking.</summary>
            public string WalletName { get; set; }
        }

        /// <summary>
        /// Information needed by the coinstake worker for finding the kernel.
        /// </summary>
        public class CoinstakeWorkerContext
        {
            /// <summary>Worker's ID / index number.</summary>
            public int Index { get; set; }

            /// <summary>Logger with worker's prefix.</summary>
            public ILogger Logger { get; set; }

            /// <summary>List of UTXO descriptions that the worker should check.</summary>
            public List<UtxoStakeDescription> utxoStakeDescriptions { get; set; }

            /// <summary>Information related to coinstake transaction.</summary>
            public CoinstakeContext CoinstakeContext { get; set; }

            /// <summary>Result shared by all workers. A structure that determines the kernel founder and the kernel UTXO that satisfies the target difficulty.</summary>
            public CoinstakeWorkerResult Result { get; set; }
        }

        /// <summary>
        /// Result of a task of coinstake worker that looks for kernel.
        /// </summary>
        public class CoinstakeWorkerResult
        {
            /// <summary>Invalid worker index as a sign that kernel was not found.</summary>
            public const int KernelNotFound = -1;

            /// <summary>Index of the worker that found the index, or <see cref="KernelNotFound"/> if no one found the kernel (yet).</summary>
            private int kernelFoundIndex;

            /// <summary>Index of the worker that found the index, or <see cref="KernelNotFound"/> if no one found the kernel (yet).</summary>
            public int KernelFoundIndex
            {
                get { return this.kernelFoundIndex; }
            }

            /// <summary>UTXO that satisfied the target difficulty.</summary>
            public UtxoStakeDescription KernelCoin { get; set; }

            /// <summary>
            /// Initializes an instance of the object.
            /// </summary>
            public CoinstakeWorkerResult()
            {
                this.kernelFoundIndex = KernelNotFound;
                this.KernelCoin = null;
            }

            /// <summary>
            /// Sets the founder of the kernel in thread-safe manner.
            /// </summary>
            /// <param name="WorkerIndex">Worker's index to set as the founder of the kernel.</param>
            /// <returns><c>true</c> if the worker's index was set as the kernel founder, <c>false</c> if another worker index was set earlier.</returns>
            public bool SetKernelFoundIndex(int WorkerIndex)
            {
                return Interlocked.CompareExchange(ref this.kernelFoundIndex, WorkerIndex, KernelNotFound) == KernelNotFound;
            }
        }

        /// <summary>
        /// Information about coinstake transaction and its private key.
        /// </summary>
        public class CoinstakeContext
        {
            /// <summary>Coinstake transaction being constructed.</summary>
            public Transaction CoinstakeTx { get; set; }

            /// <summary>If the function succeeds, this is filled with private key for signing the coinstake kernel.</summary>
            public Key Key { get; set; }
        }

        /// <summary>The maximum allowed size for a serialized block, in bytes (network rule).</summary>
        public const int MaxBlockSize = 1000000;

        ///<summary>The maximum size for mined blocks.</summary>
        public const int MaxBlockSizeGen = MaxBlockSize / 2;

        /// <summary><c>true</c> if coinstake transaction splits the coin and generates extra UTXO
        /// to prevent halting chain; <c>false</c> to disable coinstake splitting.</summary>
        /// <remarks>TODO: It should be configurable option, not constant. <see cref="https://github.com/stratisproject/StratisBitcoinFullNode/issues/550"/></remarks>
        public const bool CoinstakeSplitEnabled = true;

        /// <summary> If <see cref="CoinstakeSplitEnabled"/> is set, the coinstake will be split if
        /// the number of non-empty UTXOs in the wallet is lower than the required coin age for staking plus 1,
        /// multiplied by this value. See <see cref="GetSplitStake(int)"/>.</summary>
        public const int CoinstakeSplitLimitMultiplier = 3;

        /// <summary>Number of UTXO descriptions that a single worker's task will process.</summary>
        /// <remarks>To achieve a good level of parallelism, this should be low enough so that CPU threads are used,
        /// but high enough to compensate for tasks' overhead.</remarks>
        private const int UtxoStakeDescriptionsPerCoinstakeWorker = 25;

        /// <summary>Consumes incoming blocks, validates and executes them.</summary>
        private readonly ConsensusLoop consensusLoop;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Provider of information about the node's connection to it's network peers.</summary>
        /// <remarks>Used to verify that node is connected to network before we start staking.</remarks>
        private readonly IConnectionManager connection;

        /// <summary>Used to verify that node is not in a state of IBD (Initial Block Download).</summary>
        private readonly ChainState chainState;

        /// <summary>Provides date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Provides an interface for creating block templates of different types.</summary>
        private readonly AssemblerFactory blockAssemblerFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Consensus' view of UTXO set.</summary>
        private readonly CoinView coinView;

        /// <summary>Database of stake related data for the current blockchain.</summary>
        private readonly StakeChain stakeChain;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private readonly StakeValidator stakeValidator;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>A manager providing operations on wallets.</summary>
        private readonly WalletManager walletManager;

        /// <summary>Provides value for PoS reward and checks PoS kernel.</summary>
        private readonly PosConsensusValidator posConsensusValidator;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Loop in which the node attempts to generate new POS blocks by staking coins from its wallet.</summary>
        private IAsyncLoop stakingLoop;

        /// <summary>
        /// Target reserved balance that will not participate in staking.
        /// It is possible that less than this amount will be reserved.
        /// </summary>
        private Money targetReserveBalance;

        /// <summary>Time in milliseconds between attempts to generate PoS blocks.</summary>
        private readonly int minerSleep;

        /// <summary>A lock for managing asynchronous access to memory pool.</summary>
        protected readonly MempoolSchedulerLock mempoolLock;

        /// <summary>Memory pool of pending transactions.</summary>
        protected readonly TxMempool mempool;

        /// <summary>Information about node's staking for RPC "getstakinginfo" command.</summary>
        /// <remarks>This object does not need a synchronized access because there is no execution logic
        /// that depends on the reported information.</remarks>
        private readonly Models.GetStakingInfoModel rpcGetStakingInfoModel;

        /// <summary>Estimation of the total staking weight of all nodes on the network.</summary>
        private long networkWeight;

        /// <summary>
        /// Timestamp of the last attempt to search for POS solution.
        /// <para>
        /// It is used to prevent searching for solutions that were already proved wrong in the past.
        /// If there is no new block since last time we searched for the solution, it does not make
        /// sense to try timestamps earlier than this value.
        /// </para>
        /// </summary>
        private long lastCoinStakeSearchTime;

        /// <summary>
        /// Hash of the block headers of the block that was at the tip of the chain during our last
        /// search for POS solution.
        /// <para>
        /// It is used to prevent searching for solutions that were already proved wrong in the past.
        /// If the tip changes, <see cref="lastCoinStakeSearchTime"/> is set to the new tip's header hash.
        /// </para>
        /// </summary>
        private uint256 lastCoinStakeSearchPrevBlockHash;

        /// <summary>
        /// Initializes a new instance of the <see cref="PosMinting"/> class.
        /// </summary>
        /// <param name="consensusLoop">Consumes incoming blocks, validates and executes them.</param>
        /// <param name="chain">Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="connection">Provider of information about the node's connection to it's network peers.</param>
        /// <param name="dateTimeProvider">Provides date time functionality.</param>
        /// <param name="blockAssemblerFactory">Provides an interface for creating block templates of different types.</param>
        /// <param name="chainState">Used to verify that node is not in a state of IBD (Initial Block Download).</param>
        /// <param name="nodeLifetime">Global application life cycle control - triggers when application shuts down.</param>
        /// <param name="coinView">Consensus' view of UTXO set.</param>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="stakeValidator">Provides functionality for checking validity of PoS blocks.</param>
        /// <param name="mempoolLock">A lock for managing asynchronous access to memory pool.</param>
        /// <param name="mempool">Memory pool of pending transactions.</param>
        /// <param name="wallet">A manager providing operations on wallets.</param>
        /// <param name="asyncLoopFactory">Factory for creating background async loop tasks.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public PosMinting(
            ConsensusLoop consensusLoop,
            ConcurrentChain chain,
            Network network,
            IConnectionManager connection,
            IDateTimeProvider dateTimeProvider,
            AssemblerFactory blockAssemblerFactory,
            ChainState chainState,
            INodeLifetime nodeLifetime,
            CoinView coinView,
            StakeChain stakeChain,
            StakeValidator stakeValidator,
            MempoolSchedulerLock mempoolLock,
            TxMempool mempool,
            IWalletManager wallet,
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory)
        {
            this.consensusLoop = consensusLoop;
            this.chain = chain;
            this.network = network;
            this.connection = connection;
            this.dateTimeProvider = dateTimeProvider;
            this.blockAssemblerFactory = blockAssemblerFactory;
            this.chainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.coinView = coinView;
            this.stakeChain = stakeChain;
            this.stakeValidator = stakeValidator;
            this.mempoolLock = mempoolLock;
            this.mempool = mempool;
            this.asyncLoopFactory = asyncLoopFactory;
            this.walletManager = wallet as WalletManager;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.minerSleep = 500; // GetArg("-minersleep", 500);
            this.lastCoinStakeSearchTime = this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            this.lastCoinStakeSearchPrevBlockHash = 0;
            this.targetReserveBalance = 0; // TOOD:settings.targetReserveBalance

            this.posConsensusValidator = consensusLoop.Validator as PosConsensusValidator;

            this.rpcGetStakingInfoModel = new Miner.Models.GetStakingInfoModel();
        }
        
        /// <inheritdoc/>
        public IAsyncLoop Stake(WalletSecret walletSecret)
        {
            this.logger.LogTrace("()");
            if (this.stakingLoop != null)
            {
                this.logger.LogTrace("(-)[ALREADY_MINING]");
                return this.stakingLoop;
            }

            this.rpcGetStakingInfoModel.Enabled = true;

            this.stakingLoop = this.asyncLoopFactory.Run("PosMining.Stake", async token =>
            {
                this.logger.LogTrace("()");

                try
                {
                    await this.GenerateBlocksAsync(walletSecret).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Application stopping, nothing to do as the loop will be stopped.
                }
                catch (MinerException me)
                {
                    // Miner exceptions should be ignored. It means that the miner
                    // possibly mined a block that was not accepted by peers or is even invalid,
                    // but it should not halted the staking operation.
                    this.logger.LogDebug("Miner exception occurred in miner loop: {0}", me.ToString());
                    this.rpcGetStakingInfoModel.Errors = me.Message;
                }
                catch (ConsensusErrorException cee)
                {
                    // All consensus exceptions should be ignored. It means that the miner
                    // run into problems while constructing block or verifying it
                    // but it should not halted the staking operation.
                    this.logger.LogDebug("Consensus error exception occurred in miner loop: {0}", cee.ToString());
                    this.rpcGetStakingInfoModel.Errors = cee.Message;
                }
                catch
                {
                    this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                    throw;
                }

                this.logger.LogTrace("(-)");
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMilliseconds(this.minerSleep),
            startAfter: TimeSpans.TenSeconds);

            this.logger.LogTrace("(-)");
            return this.stakingLoop;
        }

        ///<inheritdoc/>
        public async Task GenerateBlocksAsync(WalletSecret walletSecret)
        {
            this.logger.LogTrace("()");

            BlockTemplate blockTemplate = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                while (!this.connection.ConnectedNodes.Any() || this.chainState.IsInitialBlockDownload)
                {
                    if (!this.connection.ConnectedNodes.Any()) this.logger.LogTrace("Waiting to be connected with at least one network peer...");
                    else this.logger.LogTrace("Waiting for IBD to complete...");

                    await Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                }

                ChainedBlock chainTip = this.chain.Tip;
                if (chainTip != this.consensusLoop.Tip)
                {
                    this.logger.LogTrace("(-)[SYNC_OR_REORG]");
                    return;
                }

                if (this.lastCoinStakeSearchPrevBlockHash != chainTip.HashBlock)
                {
                    this.lastCoinStakeSearchPrevBlockHash = chainTip.HashBlock;
                    this.lastCoinStakeSearchTime = chainTip.Header.Time;
                    this.logger.LogTrace("New block '{0}' detected, setting last search time to its timestamp {1}.", chainTip, chainTip.Header.Time);

                    // Reset the template as the chain advanced.
                    blockTemplate = null;
                }

                uint coinstakeTimestamp = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp() & ~PosConsensusValidator.StakeTimestampMask;
                if (coinstakeTimestamp <= this.lastCoinStakeSearchTime)
                {
                    this.logger.LogTrace("Current coinstake time {0} is not greater than last search timestamp {1}.", coinstakeTimestamp, this.lastCoinStakeSearchTime);
                    this.logger.LogTrace("(-)[NOTHING_TO_DO]");
                    return;
                }

                var utxoStakeDescriptions = new List<UtxoStakeDescription>();
                IEnumerable<UnspentOutputReference> spendable = this.walletManager.GetSpendableTransactionsInWallet(walletSecret.WalletName, 1);

                FetchCoinsResponse coinset = await this.coinView.FetchCoinsAsync(spendable.Select(t => t.Transaction.Id).ToArray()).ConfigureAwait(false);

                long totalBalance = 0;
                foreach (UnspentOutputReference infoTransaction in spendable)
                {
                    UnspentOutputs set = coinset.UnspentOutputs.FirstOrDefault(f => f?.TransactionId == infoTransaction.Transaction.Id);
                    TxOut utxo = (set != null) && (infoTransaction.Transaction.Index < set.Outputs.Length) ? set.Outputs[infoTransaction.Transaction.Index] : null;

                    if ((utxo != null) && (utxo.Value > Money.Zero))
                    {
                        var utxoStakeDescription = new UtxoStakeDescription();

                        utxoStakeDescription.TxOut = utxo;
                        utxoStakeDescription.OutPoint = new OutPoint(set.TransactionId, infoTransaction.Transaction.Index);
                        utxoStakeDescription.Address = infoTransaction.Address;
                        utxoStakeDescription.HashBlock = this.chain.GetBlock((int)set.Height).HashBlock;
                        utxoStakeDescription.UtxoSet = set;
                        utxoStakeDescription.Secret = walletSecret; // Temporary.
                        utxoStakeDescriptions.Add(utxoStakeDescription);

                        totalBalance += utxo.Value;
                        this.logger.LogTrace("UTXO '{0}' with value {1} might be available for staking.", utxoStakeDescription.OutPoint, utxo.Value);
                    }
                }

                this.logger.LogTrace("Wallet contains {0} coins.", new Money(totalBalance));

                if (blockTemplate == null)
                    blockTemplate = this.blockAssemblerFactory.Create(chainTip, new AssemblerOptions() { IsProofOfStake = true }).CreateNewBlock(new Script());

                Block block = blockTemplate.Block;

                this.networkWeight = (long)this.GetNetworkWeight();
                this.rpcGetStakingInfoModel.CurrentBlockSize = block.GetSerializedSize();
                this.rpcGetStakingInfoModel.CurrentBlockTx = block.Transactions.Count();
                this.rpcGetStakingInfoModel.PooledTx = await this.mempoolLock.ReadAsync(() => this.mempool.MapTx.Count).ConfigureAwait(false);
                this.rpcGetStakingInfoModel.Difficulty = this.GetDifficulty(chainTip);
                this.rpcGetStakingInfoModel.NetStakeWeight = this.networkWeight;

                // Trying to create coinstake that satisfies the difficulty target, put it into a block and sign the block.
                if (await this.StakeAndSignBlockAsync(utxoStakeDescriptions, block, chainTip, blockTemplate.TotalFee, coinstakeTimestamp).ConfigureAwait(false))
                {
                    this.logger.LogTrace("New POS block created and signed successfully.");
                    this.CheckStake(block, chainTip);

                    blockTemplate = null;
                }
                else
                {
                    this.logger.LogTrace("{0} failed, waiting {1} ms for next round...", nameof(this.StakeAndSignBlockAsync), this.minerSleep);
                    await Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// One a new block is staked, this method is used to verify that it
        /// is a valid block and if so, it will add it to the chain.
        /// </summary>
        /// <param name="block">The new block.</param>
        /// <param name="chainTip">Block that was considered as a chain tip when the block staking started.</param>
        private void CheckStake(Block block, ChainedBlock chainTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainTip), chainTip);

            if (!BlockStake.IsProofOfStake(block))
            {
                this.logger.LogTrace("(-)[NOT_POS]");
                return;
            }

            // Verify hash target and signature of coinstake tx.
            BlockStake prevBlockStake = this.stakeChain.Get(chainTip.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[NO_PREV_STAKE]");
                ConsensusErrors.PrevStakeNull.Throw();
            }

            // Validate the block.
            var blockValidationContext = new BlockValidationContext { Block = block };
            this.consensusLoop.AcceptBlockAsync(blockValidationContext).GetAwaiter().GetResult();

            if (blockValidationContext.ChainedBlock == null)
            {
                this.logger.LogTrace("(-)[REORG-2]");
                return;
            }

            if (blockValidationContext.Error != null)
            {
                this.logger.LogTrace("(-)[ACCEPT_BLOCK_ERROR]");
                return;
            }

            this.logger.LogInformation("==================================================================");
            this.logger.LogInformation("Found new POS block hash '{0}' at height {1}.", blockValidationContext.ChainedBlock.HashBlock, blockValidationContext.ChainedBlock.Height);
            this.logger.LogInformation("==================================================================");
        }

        /// <summary>
        /// Attempts to find a POS staking solution and if it succeeds, then it completes a block
        /// to be mined and signes it.
        /// </summary>
        /// <param name="utxoStakeDescriptions">List of UTXOs that are available in the wallet for staking.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="chainTip">Tip of the best chain.</param>
        /// <param name="fees">Transaction fees from the transactions included in the block if we mine it.</param>
        /// <param name="coinstakeTimestamp">Maximal timestamp of the coinstake transaction. The actual timestamp can be lower, but not higher.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        private async Task<bool> StakeAndSignBlockAsync(List<UtxoStakeDescription> utxoStakeDescriptions, Block block, ChainedBlock chainTip, long fees, uint coinstakeTimestamp)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}',{5}:{6},{7}:{8})", nameof(utxoStakeDescriptions), nameof(utxoStakeDescriptions.Count), utxoStakeDescriptions.Count, nameof(chainTip), chainTip, nameof(fees), fees, nameof(coinstakeTimestamp), coinstakeTimestamp);

            // If we are trying to sign something except proof-of-stake block template.
            if (!block.Transactions[0].Outputs[0].IsEmpty)
            {
                this.logger.LogTrace("(-)[NO_POS_BLOCK]:false");
                return false;
            }

            // If we are trying to sign a complete proof-of-stake block.
            if (BlockStake.IsProofOfStake(block))
            {
                this.logger.LogTrace("(-)[ALREADY_DONE]:true");
                return true;
            }

            var coinstakeContext = new CoinstakeContext();
            coinstakeContext.CoinstakeTx = new Transaction();
            coinstakeContext.CoinstakeTx.Time = coinstakeTimestamp;

            // Search to current coinstake time.
            long searchTime = coinstakeContext.CoinstakeTx.Time;

            long searchInterval = searchTime - this.lastCoinStakeSearchTime;
            this.rpcGetStakingInfoModel.SearchInterval = (int)searchInterval;

            this.lastCoinStakeSearchTime = searchTime;
            this.logger.LogTrace("Search interval set to {0}, last coinstake search timestamp set to {1}.", searchInterval, this.lastCoinStakeSearchTime);

            if (await this.CreateCoinstakeAsync(utxoStakeDescriptions, block, chainTip, searchInterval, fees, coinstakeContext).ConfigureAwait(false))
            {
                uint minTimestamp = chainTip.Header.Time + 1;
                if (coinstakeContext.CoinstakeTx.Time >= minTimestamp)
                {
                    // Make sure coinstake would meet timestamp protocol
                    // as it would be the same as the block timestamp.
                    block.Transactions[0].Time = block.Header.Time = coinstakeContext.CoinstakeTx.Time;

                    // We have to make sure that we have no future timestamps in
                    // our transactions set.
                    foreach (Transaction transaction in block.Transactions)
                    {
                        if (transaction.Time > block.Header.Time)
                        {
                            this.logger.LogTrace("Removing transaction with timestamp {0} as it is greater than coinstake transaction timestamp {1}.", transaction.Time, block.Header.Time);
                            block.Transactions.Remove(transaction);
                        }
                    }

                    block.Transactions.Insert(1, coinstakeContext.CoinstakeTx);
                    block.UpdateMerkleRoot();

                    // Append a signature to our block.
                    ECDSASignature signature = coinstakeContext.Key.Sign(block.GetHash());

                    block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };
                    this.logger.LogTrace("(-):true");
                    return true;
                }
                else this.logger.LogTrace("Coinstake transaction created with too early timestamp {0}, minimal timestamp is {1}.", coinstakeContext.CoinstakeTx.Time, minTimestamp);
            }
            else this.logger.LogTrace("Unable to create coinstake transaction.");

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <inheritdoc/>
        public async Task<bool> CreateCoinstakeAsync(List<UtxoStakeDescription> utxoStakeDescriptions, Block block, ChainedBlock chainTip, long searchInterval, long fees, CoinstakeContext coinstakeContext)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}',{5}:{6},{7}:{8})", nameof(utxoStakeDescriptions), nameof(utxoStakeDescriptions.Count), utxoStakeDescriptions.Count, nameof(chainTip), chainTip, nameof(searchInterval), searchInterval, nameof(fees), fees);

            int nonEmptyUtxos = utxoStakeDescriptions.Count;
            coinstakeContext.CoinstakeTx.Inputs.Clear();
            coinstakeContext.CoinstakeTx.Outputs.Clear();

            // Mark coinstake transaction.
            coinstakeContext.CoinstakeTx.Outputs.Add(new TxOut(Money.Zero, new Script()));

            long balance = this.GetMatureBalance(utxoStakeDescriptions).Satoshi;
            if (balance <= this.targetReserveBalance)
            {
                this.rpcGetStakingInfoModel.Staking = false;

                this.logger.LogTrace("Total balance of available UTXOs is {0}, which is less than or equal to reserve balance {1}.", balance, this.targetReserveBalance);
                this.logger.LogTrace("(-)[BELOW_RESERVE]:false");
                return false;
            }

            // Select UTXOs with suitable depth.
            List<UtxoStakeDescription> stakingUtxoDescriptions = this.GetUtxoStakeDescriptionsSuitableForStaking(utxoStakeDescriptions, coinstakeContext.CoinstakeTx.Time, balance - this.targetReserveBalance);
            if (!stakingUtxoDescriptions.Any())
            {
                this.rpcGetStakingInfoModel.Staking = false;
                this.logger.LogTrace("(-)[NO_SELECTION]:false");
                return false;
            }

            long ourWeight = stakingUtxoDescriptions.Sum(s => s.TxOut.Value);
            long expectedTime = StakeValidator.TargetSpacingSeconds * this.networkWeight / ourWeight;
            decimal ourPercent = this.networkWeight != 0 ? 100.0m * (decimal)ourWeight / (decimal)this.networkWeight : 0;

            this.logger.LogInformation("Node staking with {0} ({1:0.00} % of the network weight {2}), est. time to find new block is {3}.", new Money(ourWeight), ourPercent, new Money(this.networkWeight), TimeSpan.FromSeconds(expectedTime));

            this.rpcGetStakingInfoModel.Staking = true;
            this.rpcGetStakingInfoModel.Weight = ourWeight;
            this.rpcGetStakingInfoModel.ExpectedTime = expectedTime;
            this.rpcGetStakingInfoModel.Errors = null;

            long minimalAllowedTime = chainTip.Header.Time + 1;
            this.logger.LogTrace("Trying to find staking solution among {0} transactions, minimal allowed time is {1}, coinstake time is {2}.", stakingUtxoDescriptions.Count, minimalAllowedTime, coinstakeContext.CoinstakeTx.Time);

            // If the time after applying the mask is lower than minimal allowed time,
            // it is simply too early for us to mine, there can't be any valid solution.
            if ((coinstakeContext.CoinstakeTx.Time & ~PosConsensusValidator.StakeTimestampMask) < minimalAllowedTime)
            {
                this.logger.LogTrace("(-)[TOO_EARLY_TIME_AFTER_LAST_BLOCK]:false");
                return false;
            }

            // Create worker tasks that will look for kernel.
            // Run task in parallel using the default task scheduler.
            int coinIndex = 0;
            int workerCount = (stakingUtxoDescriptions.Count + UtxoStakeDescriptionsPerCoinstakeWorker - 1) / UtxoStakeDescriptionsPerCoinstakeWorker;
            var workers = new Task[workerCount];
            var workerContexts = new CoinstakeWorkerContext[workerCount];

            CoinstakeWorkerResult workersResult = new CoinstakeWorkerResult();
            for (int workerIndex = 0; workerIndex < workerCount; workerIndex++)
            {
                var cwc = new CoinstakeWorkerContext
                {
                    Index = workerIndex,
                    Logger = this.loggerFactory.CreateLogger(this.GetType().FullName, $"[Worker #{workerIndex}] "),
                    utxoStakeDescriptions = new List<UtxoStakeDescription>(),
                    CoinstakeContext = coinstakeContext,
                    Result = workersResult
                };

                int stakingUtxoCount = Math.Min(stakingUtxoDescriptions.Count - coinIndex, UtxoStakeDescriptionsPerCoinstakeWorker);
                cwc.utxoStakeDescriptions.AddRange(stakingUtxoDescriptions.GetRange(coinIndex, stakingUtxoCount));
                coinIndex += stakingUtxoCount;
                workerContexts[workerIndex] = cwc;

                workers[workerIndex] = Task.Run(() => this.CoinstakeWorker(cwc, chainTip, block, minimalAllowedTime, searchInterval));
            }

            await Task.WhenAll(workers).ConfigureAwait(false);

            if (workersResult.KernelFoundIndex == CoinstakeWorkerResult.KernelNotFound)
            {
                this.logger.LogTrace("(-)[KERNEL_NOT_FOUND]:false");
                return false;
            }

            this.logger.LogTrace("Worker #{0} found the kernel.", workersResult.KernelFoundIndex);

            long reward = fees + this.posConsensusValidator.GetProofOfStakeReward(chainTip.Height);
            if (reward <= 0)
            {
                // TODO: This can't happen unless we remove reward for mined block.
                // If this can happen over time then this check could be done much sooner
                // to avoid a lot of computation.
                this.logger.LogTrace("(-)[NO_REWARD]:false");
                return false;
            }

            // Split stake if above threshold.
            bool splitStake = this.GetSplitStake(nonEmptyUtxos);
            if (splitStake)
            {
                this.logger.LogTrace("Coinstake UTXO will be split to two.");
                coinstakeContext.CoinstakeTx.Outputs.Add(new TxOut(0, coinstakeContext.CoinstakeTx.Outputs[1].ScriptPubKey));
            }

            // Input to coinstake transaction.
            UtxoStakeDescription coinstakeInput = workersResult.KernelCoin;

            // Total amount of input values in coinstake transaction.
            long coinstakeInputValue = coinstakeInput.TxOut.Value + reward;

            // Set output amount.
            if (coinstakeContext.CoinstakeTx.Outputs.Count == 3)
            {
                coinstakeContext.CoinstakeTx.Outputs[1].Value = (coinstakeInputValue / 2 / Money.CENT) * Money.CENT;
                coinstakeContext.CoinstakeTx.Outputs[2].Value = coinstakeInputValue - coinstakeContext.CoinstakeTx.Outputs[1].Value;
                this.logger.LogTrace("Coinstake first output value is {0}, second is {1}.", coinstakeContext.CoinstakeTx.Outputs[1].Value, coinstakeContext.CoinstakeTx.Outputs[2].Value);
            }
            else
            {
                coinstakeContext.CoinstakeTx.Outputs[1].Value = coinstakeInputValue;
                this.logger.LogTrace("Coinstake output value is {0}.", coinstakeContext.CoinstakeTx.Outputs[1].Value);
            }

            // Sign.
            if (!this.SignTransactionInput(coinstakeInput, coinstakeContext.CoinstakeTx))
            {
                this.logger.LogTrace("(-)[SIGN_FAILED]:false");
                return false;
            }

            // Limit size.
            int serializedSize = coinstakeContext.CoinstakeTx.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION, SerializationType.Network);
            if (serializedSize >= (MaxBlockSizeGen / 5))
            {
                this.logger.LogTrace("Coinstake size {0} bytes exceeded limit {1} bytes.", serializedSize, MaxBlockSizeGen / 5);
                this.logger.LogTrace("(-)[SIZE_EXCEEDED]:false");
                return false;
            }

            // Successfully generated coinstake.
            this.logger.LogTrace("(-):true");
            return true;
        }

        /// <summary>
        /// Worker method that tries to find coinstake kernel within a small list of UTXOs.
        /// <para>
        /// There are multiple worker tasks created, each checking subset of all available UTXOs.
        /// This allows the kernel finding task to be processed on multiple processors in parallel.
        /// </para>
        /// </summary>
        /// <param name="context">Context information with worker task description. Results of the worker's attempt are also stored in this context.</param>
        /// <param name="chainTip">Tip of the best chain. Used only to stop working as soon as the chain advances.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="minimalAllowedTime">Minimal valid timestamp for new coinstake transaction.</param>
        /// <param name="searchInterval">Length of an unexplored block time space in seconds. It only makes sense to look for a solution within this interval.</param>
        private void CoinstakeWorker(CoinstakeWorkerContext context, ChainedBlock chainTip, Block block, long minimalAllowedTime, long searchInterval)
        {
            context.Logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})", nameof(chainTip), chainTip, nameof(minimalAllowedTime), minimalAllowedTime, nameof(searchInterval), searchInterval);

            context.Logger.LogTrace("Going to process {0} UTXOs.", context.utxoStakeDescriptions.Count);

            // Sort staking UTXOs by amount, so that highest amounts are tried first
            // because they have greater chance to succeed and thus saving some work.
            List<UtxoStakeDescription> orderedUtxoStakeDescriptions = context.utxoStakeDescriptions.OrderByDescending(o => o.TxOut.Value).ToList();

            bool stopWork = false;
            foreach (UtxoStakeDescription utxoStakeInfo in orderedUtxoStakeDescriptions)
            {
                context.Logger.LogTrace("Trying UTXO from address '{0}', output amount {1}...", utxoStakeInfo.Address.Address, utxoStakeInfo.TxOut.Value);

                // Script of the first coinstake input.
                Script scriptPubKeyKernel = utxoStakeInfo.TxOut.ScriptPubKey;
                if (!PayToPubkeyTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel)
                    && !PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                {
                    context.Logger.LogTrace("Kernel type must be P2PK or P2PKH, kernel rejected.");
                    continue;
                }

                for (uint n = 0; n < searchInterval; n++)
                {
                    if (context.Result.KernelFoundIndex != CoinstakeWorkerResult.KernelNotFound)
                    {
                        context.Logger.LogTrace("Different worker #{0} already found kernel, stopping work.", context.Result.KernelFoundIndex);
                        stopWork = true;
                        break;
                    }

                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        context.Logger.LogTrace("Application shutdown detected, stopping work.");
                        stopWork = true;
                        break;
                    }

                    if (chainTip != this.chain.Tip)
                    {
                        context.Logger.LogTrace("Chain advanced, stopping work.");
                        stopWork = true;
                        break;
                    }

                    uint txTime = context.CoinstakeContext.CoinstakeTx.Time - n;

                    // Once we reach previous block time + 1, we can't go any lower
                    // because it is required that the block time is greater than the previous block time.
                    if (txTime < minimalAllowedTime)
                        break;

                    if ((txTime & PosConsensusValidator.StakeTimestampMask) != 0)
                        continue;

                    context.Logger.LogTrace("Trying with transaction time {0}...", txTime);
                    try
                    {
                        var prevoutStake = new OutPoint(utxoStakeInfo.UtxoSet.TransactionId, utxoStakeInfo.OutPoint.N);

                        var contextInformation = new ContextInformation(new BlockValidationContext { Block = block }, this.network.Consensus);
                        contextInformation.SetStake();
                        this.posConsensusValidator.StakeValidator.CheckKernel(contextInformation.Stake, chainTip, block.Header.Bits, txTime, prevoutStake);

                        if (context.Result.SetKernelFoundIndex(context.Index))
                        {
                            context.Logger.LogTrace("Kernel found with solution hash '{0}'.", contextInformation.Stake.HashProofOfStake);

                            Wallet.Wallet wallet = this.walletManager.GetWalletByName(utxoStakeInfo.Secret.WalletName);
                            context.CoinstakeContext.Key = wallet.GetExtendedPrivateKeyForAddress(utxoStakeInfo.Secret.WalletPassword, utxoStakeInfo.Address).PrivateKey;

                            // Create a pubkey script form the current script.
                            Script scriptPubKeyOut = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(context.CoinstakeContext.Key.PubKey); // scriptPubKeyKernel

                            utxoStakeInfo.Key = context.CoinstakeContext.Key;
                            context.CoinstakeContext.CoinstakeTx.Time = txTime;
                            context.CoinstakeContext.CoinstakeTx.AddInput(new TxIn(prevoutStake));
                            context.CoinstakeContext.CoinstakeTx.Outputs.Add(new TxOut(0, scriptPubKeyOut));

                            context.Result.KernelCoin = utxoStakeInfo;

                            context.Logger.LogTrace("Kernel accepted, coinstake input is '{0}', stopping work.", prevoutStake);
                        }
                        else context.Logger.LogTrace("Kernel found, but worker #{0} announced its kernel earlier, stopping work.", context.Result.KernelFoundIndex);

                        stopWork = true;
                    }
                    catch (ConsensusErrorException cex)
                    {
                        context.Logger.LogTrace("Checking kernel failed with exception: {0}.", cex.Message);
                        if (cex.ConsensusError == ConsensusErrors.StakeHashInvalidTarget)
                            continue;

                        stopWork = true;
                    }

                    if (stopWork) break;
                }

                // If kernel is found or error occurred, stop searching.
                if (stopWork) break;
            }

            context.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Signs input of a transaction.
        /// </summary>
        /// <param name="input">Transaction input.</param>
        /// <param name="transaction">Transaction being built.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        private bool SignTransactionInput(UtxoStakeDescription input, Transaction transaction)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(input), input.OutPoint);

            bool res = false;
            try
            {
                new TransactionBuilder()
                    .AddKeys(input.Key)
                    .AddCoins(new Coin(input.OutPoint, input.TxOut))
                    .SignTransactionInPlace(transaction);

                res = true;
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Exception occurred: {0}", e.ToString());
            }

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
        
        /// <inheritdoc/>
        public Money GetMatureBalance(List<UtxoStakeDescription> utxoStakeDescriptions)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(utxoStakeDescriptions), nameof(utxoStakeDescriptions.Count), utxoStakeDescriptions.Count);

            var money = new Money(0);
            foreach (UtxoStakeDescription utxoStakeDescription in utxoStakeDescriptions)
            {
                // Must wait until coinbase is safely deep enough in the chain before valuing it.
                if ((utxoStakeDescription.UtxoSet.IsCoinbase || utxoStakeDescription.UtxoSet.IsCoinstake) && (this.GetBlocksCountToMaturity(utxoStakeDescription) > 0))
                    continue;

                money += utxoStakeDescription.TxOut.Value;
            }

            this.logger.LogTrace("(-):{0}", money);
            return money;
        }

        /// <summary>
        /// Selects UTXOs that are suitable for staking.
        /// <para>
        /// Such a UTXO has to be confirmed with enough confirmations - i.e. has suitable depth,
        /// and it also has to be mature and meet requirement for minimal value.
        /// </para>
        /// </summary>
        /// <param name="utxoStakeDescriptions">List of UTXO descriptions that are candidates for being used for staking.</param>
        /// <param name="spendTime">Timestamp of the coinstake transaction.</param>
        /// <param name="targetValue">Target money amount of UTXOs that can be used for staking.</param>
        /// <returns>List of UTXO descriptions that meet the requirements for staking.</returns>
        private List<UtxoStakeDescription> GetUtxoStakeDescriptionsSuitableForStaking(List<UtxoStakeDescription> utxoStakeDescriptions, uint spendTime, long targetValue)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4},{5}:{6})", nameof(utxoStakeDescriptions), nameof(utxoStakeDescriptions.Count), utxoStakeDescriptions.Count, nameof(spendTime), spendTime, nameof(targetValue), targetValue);
            var res = new List<UtxoStakeDescription>();

            long currentValue = 0;
            long requiredDepth = this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations;
            foreach (UtxoStakeDescription utxoStakeDescription in utxoStakeDescriptions.OrderByDescending(x => x.TxOut.Value))
            {
                int depth = this.GetDepthInMainChain(utxoStakeDescription);
                this.logger.LogTrace("Checking if UTXO '{0}' value {1} can be added, its depth is {2}.", utxoStakeDescription.OutPoint, utxoStakeDescription.TxOut.Value, depth);

                if (depth < 1)
                {
                    this.logger.LogTrace("UTXO '{0}' is new or reorg happened.", utxoStakeDescription.OutPoint);
                    continue;
                }

                if (depth < requiredDepth)
                {
                    this.logger.LogTrace("UTXO '{0}' depth {1} is lower than required minimum depth {2}.", utxoStakeDescription.OutPoint, depth, requiredDepth);
                    continue;
                }

                if (utxoStakeDescription.UtxoSet.Time > spendTime)
                {
                    this.logger.LogTrace("UTXO '{0}' can't be added because its time {1} is greater than coinstake time {2}.", utxoStakeDescription.OutPoint, utxoStakeDescription.UtxoSet.Time, spendTime);
                    continue;
                }

                int toMaturity = this.GetBlocksCountToMaturity(utxoStakeDescription);
                if (toMaturity > 0)
                {
                    this.logger.LogTrace("UTXO '{0}' can't be added because it is not mature, {1} blocks to maturity left.", utxoStakeDescription.OutPoint, toMaturity);
                    continue;
                }

                currentValue += utxoStakeDescription.TxOut.Value;

                this.logger.LogTrace("UTXO '{0}' accepted.", utxoStakeDescription.OutPoint);
                res.Add(utxoStakeDescription);

                if (currentValue >= targetValue)
                    break;
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(res.Count), res.Count);
            return res;
        }

        /// <summary>
        /// Calculates blocks count till UTXO is considered mature for staking.
        /// </summary>
        /// <param name="utxoStakeDescription">The UTXO stake description.</param>
        /// <returns>How many blocks are left till UTXO is considered mature for staking.</returns>
        private int GetBlocksCountToMaturity(UtxoStakeDescription utxoStakeDescription)
        {
            if (!(utxoStakeDescription.UtxoSet.IsCoinbase || utxoStakeDescription.UtxoSet.IsCoinstake))
                return 0;

            return Math.Max(0, (int)this.network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity + 1 - this.GetDepthInMainChain(utxoStakeDescription));
        }

        /// <summary>
        /// Gets depth of transaction in blockchain.
        /// </summary>
        /// <param name="utxoStakeDescription">The UTXO stake description.</param>
        /// <returns>
        /// <c>-1</c> if not in blockchain, and not in memory pool (conflicted transaction).
        /// <c>0</c> if in memory pool, waiting to be included in a block.
        /// Value greater than <c>1</c> if included in a block. Shows how many blocks deep in the main chain.
        /// </returns>
        private int GetDepthInMainChain(UtxoStakeDescription utxoStakeDescription)
        {
            ChainedBlock chainedBlock = this.chain.GetBlock(utxoStakeDescription.HashBlock);

            if (chainedBlock == null)
                return -1;

            // TODO: Check if in memory pool then return 0.
            return this.chain.Tip.Height - chainedBlock.Height + 1;
        }

        ///<inheritdoc/>
        public double GetDifficulty(ChainedBlock block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block);

            double res = 1.0;

            if (block == null)
            {
                // Use consensus loop's tip rather than concurrent chain's tip
                // because consensus loop's tip is guaranteed to have block stake in the database.
                ChainedBlock tip = this.consensusLoop.Tip;
                if (tip == null)
                {
                    this.logger.LogTrace("(-)[DEFAULT]:{0}", res);
                    return res;
                }

                block = this.stakeValidator.GetLastPowPosChainedBlock(this.stakeChain, tip, false);
            }

            uint shift = (block.Header.Bits >> 24) & 0xFF;
            double diff = (double)0x0000FFFF / (double)(block.Header.Bits & 0x00FFFFFF);

            while (shift < 29)
            {
                diff *= 256.0;
                shift++;
            }

            while (shift > 29)
            {
                diff /= 256.0;
                shift--;
            }

            res = diff;
            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
        
        /// <inheritdoc/>
        public double GetNetworkWeight()
        {
            this.logger.LogTrace("()");
            int interval = 72;
            double stakeKernelsAvg = 0.0;
            int stakesHandled = 0;
            long stakesTime = 0;

            // Use consensus loop's tip rather than concurrent chain's tip
            // because consensus loop's tip is guaranteed to have block stake in the database.
            ChainedBlock block = this.consensusLoop.Tip;
            ChainedBlock prevStakeBlock = null;

            double res = 0.0;
            while ((block != null) && (stakesHandled < interval))
            {
                BlockStake blockStake = this.stakeChain.Get(block.HashBlock);
                if (blockStake.IsProofOfStake())
                {
                    if (prevStakeBlock != null)
                    {
                        stakeKernelsAvg += this.GetDifficulty(prevStakeBlock) * (double)0x100000000;
                        stakesTime += (long)prevStakeBlock.Header.Time - (long)block.Header.Time;
                        stakesHandled++;
                    }

                    prevStakeBlock = block;
                }

                block = block.Previous;
            }

            if (stakesTime != 0) res = stakeKernelsAvg / stakesTime;

            res *= PosConsensusValidator.StakeTimestampMask + 1;

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
        
        /// <inheritdoc/>
        public Models.GetStakingInfoModel GetGetStakingInfoModel()
        {
            return (Models.GetStakingInfoModel)this.rpcGetStakingInfoModel.Clone();
        }

        /// <summary>
        /// Checks whether the coinstake should be split or not.
        /// </summary>
        /// <param name="utxoCount">Number of non-empty UTXOs in the wallet.</param>
        /// <returns><c>true</c> if the coinstake should be split, <c>false</c> otherwise.</returns>
        /// <remarks>The coinstake is split if the number of non-empty UTXOs we have in the wallet
        /// is under the given threshold.</remarks>
        /// <seealso cref="CoinstakeSplitLimitMultiplier"/>
        private bool GetSplitStake(int utxoCount)
        {
            this.logger.LogTrace("({0}:{1})", nameof(utxoCount), utxoCount);

            long maturityLimit = this.network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity;
            long coinAgeLimit = this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations;
            long requiredCoinAgeForStaking = Math.Max(maturityLimit, coinAgeLimit);
            this.logger.LogTrace("Required coin age for staking is {0}.", requiredCoinAgeForStaking);

            bool res = utxoCount < (requiredCoinAgeForStaking + 1) * CoinstakeSplitLimitMultiplier;

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }
    }
}
