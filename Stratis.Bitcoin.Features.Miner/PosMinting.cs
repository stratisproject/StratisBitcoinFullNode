using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;

namespace Stratis.Bitcoin.Features.Miner
{
    public class PosMinting
    {
        public class StakeOutput
        {
            public StakeTx StakeTx;
            public int Depth;
        }

        public class StakeTx
        {
            public uint256 HashBlock;
            public TxOut TxOut;
            public OutPoint OutPoint;
            public int OutputIndex;
            public HdAddress Address;
            public UnspentOutputs UtxoSet;
            public WalletSecret Secret;
            public Key Key;
        }

        public class WalletSecret
        {
            public string WalletPassword;
            public string WalletName;
        }

        // Default for -blockmintxfee, which sets the minimum feerate for a transaction in blocks created by mining code 
        public const int DefaultBlockMinTxFee = 1000;

        // Default for -blockmaxsize, which controls the maximum size of block the mining code will create 
        public const int DefaultBlockMaxSize = 750000;

        // Default for -blockmaxweight, which controls the range of block weights the mining code will create 
        public const int DefaultBlockMaxWeight = 3000000;

        /** The maximum allowed size for a serialized block, in bytes (network rule) */
        public const int MaxBlockSize = 1000000;
        /** The maximum size for mined blocks */
        public const int MaxBlockSizeGen = MaxBlockSize / 2;

        private readonly ConsensusLoop consensusLoop;
        private readonly ConcurrentChain chain;
        private readonly Network network;
        private readonly IConnectionManager connection;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly AssemblerFactory blockAssemblerFactory;
        private readonly IBlockRepository blockRepository;
        private readonly ChainState chainState;
        private readonly Signals.Signals signals;
        private readonly INodeLifetime nodeLifetime;
        private readonly NodeSettings settings;
        private readonly CoinView coinView;
        private readonly StakeChain stakeChain;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly WalletManager walletManager;
        private readonly PosConsensusValidator posConsensusValidator;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private IAsyncLoop mining;
        private Money reserveBalance;
        private readonly int minimumInputValue;
        private readonly int minerSleep;

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

        public PosMinting(
            ConsensusLoop consensusLoop,
            ConcurrentChain chain,
            Network network,
            IConnectionManager connection,
            IDateTimeProvider dateTimeProvider,
            AssemblerFactory blockAssemblerFactory,
            IBlockRepository blockRepository,
            ChainState chainState,
            Signals.Signals signals, INodeLifetime nodeLifetime,
            NodeSettings settings,
            CoinView coinView,
            StakeChain stakeChain,
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
            this.blockRepository = blockRepository;
            this.chainState = chainState;
            this.signals = signals;
            this.nodeLifetime = nodeLifetime;
            this.settings = settings;
            this.coinView = coinView;
            this.stakeChain = stakeChain;
            this.asyncLoopFactory = asyncLoopFactory;
            this.walletManager = wallet as WalletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.minerSleep = 500; // GetArg("-minersleep", 500);
            this.lastCoinStakeSearchTime = this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            this.lastCoinStakeSearchPrevBlockHash = 0;
            this.reserveBalance = 0; // TOOD:settings.ReserveBalance 
            this.minimumInputValue = 0;

            this.posConsensusValidator = consensusLoop.Validator as PosConsensusValidator;
        }

        public IAsyncLoop Mine(WalletSecret walletSecret)
        {
            this.logger.LogTrace("()");
            if (this.mining != null)
            {
                this.logger.LogTrace("(-)[ALREADY_MINING]");
                return this.mining;
            }

            this.mining = this.asyncLoopFactory.Run("PosMining.Mine", token =>
            {
                this.logger.LogTrace("()");

                try
                {
                    this.GenerateBlocks(walletSecret);
                }
                catch (OperationCanceledException)
                {
                    // Application stopping, nothing to do as the loop will be stopped.
                }
                catch (MinerException me)
                {
                    // Miner rexceptions should be ignored. It means that the miner 
                    // possibly mined a block that was not accepted by peers or is even invalid,
                    // but it should not halted the mining operation.
                    this.logger.LogDebug("Miner exception occurred in miner loop: {0}", me.ToString());
                }
                catch (ConsensusErrorException cee)
                {
                    // All consensus exceptions should be ignored. It means that the miner 
                    // run into problems while constructing block or verifying it
                    // but it should not halted the mining operation.
                    this.logger.LogDebug("Consensus error exception occurred in miner loop: {0}", cee.ToString());
                }
                catch (Exception e)
                {
                    this.logger.LogTrace("(-)[UNHANDLED_EXCEPTION]");
                    throw e;
                }

                this.logger.LogTrace("(-)");
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMilliseconds(this.minerSleep),
            startAfter: TimeSpans.TenSeconds);

            this.logger.LogTrace("(-)");
            return this.mining;
        }

        public void GenerateBlocks(WalletSecret walletSecret)
        {
            this.logger.LogTrace("()");

            BlockTemplate blockTemplate = null;
            bool tryToSync = true;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                ChainedBlock chainTip = this.chain.Tip;
                if (chainTip != this.consensusLoop.Tip)
                {
                    this.logger.LogTrace("(-)[SYNC_OR_REORG]");
                    return;
                }

                while (!this.connection.ConnectedNodes.Any() || this.chainState.IsInitialBlockDownload)
                {
                    if (!this.connection.ConnectedNodes.Any()) this.logger.LogTrace("Waiting to be connected with at least one network peer...");
                    else this.logger.LogTrace("Waiting for IBD to complete...");

                    tryToSync = true;
                    Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }

                // TODO: What is the purpose of this conditional block?
                // It seems that if it ends with continue, the next round of the loop will just 
                // not execute the above while loop and it won't even enter this block again,
                // so it seems like no operation.
                // In StratisX there is the wait uncommented. Then it makes sense. Why we have it commented?
                if (tryToSync)
                {
                    tryToSync = false;
                    // TODO: This condition to have at least 3 peers is disqualifying us on testnet quite often.
                    // Yet the 60 secs delay does not prevent us to mine because tryToSync will be false next time we are here
                    // unless we are completely disconnected. So this is weird logic.
                    bool fewPeers = this.connection.ConnectedNodes.Count() < 3;
                    bool lastBlockTooOld = chainTip.Header.Time < (this.dateTimeProvider.GetTime() - 10 * 60);
                    if ((fewPeers && !this.network.IsTest()) || lastBlockTooOld)
                    {
                        if (fewPeers) this.logger.LogTrace("Node is connected to few peers.");
                        if (lastBlockTooOld) this.logger.LogTrace("Last block is too old, timestamp {0}.", chainTip.Header.Time);

                        Task.Delay(TimeSpan.FromMilliseconds(60000), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                        continue;
                    }
                }

                ChainedBlock prevBlock = this.consensusLoop.Tip;

                if (this.lastCoinStakeSearchPrevBlockHash != prevBlock.HashBlock)
                {
                    this.lastCoinStakeSearchPrevBlockHash = prevBlock.HashBlock;
                    this.lastCoinStakeSearchTime = prevBlock.Header.Time;
                    this.logger.LogTrace("New block '{0}/{1}' detected, setting last search time to its timestamp {2}.", prevBlock.HashBlock, prevBlock.Height, prevBlock.Header.Time);
                }

                uint coinstakeTimestamp = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp() & ~PosConsensusValidator.StakeTimestampMask;
                if (coinstakeTimestamp <= this.lastCoinStakeSearchTime)
                {
                    this.logger.LogTrace("Current coinstake time {0} is not greater than last search timestamp {1}.", coinstakeTimestamp, this.lastCoinStakeSearchTime);
                    this.logger.LogTrace("(-)[NOTHING_TO_DO]");
                    return;
                }

                if (blockTemplate == null)
                    blockTemplate = this.blockAssemblerFactory.Create(new AssemblerOptions() { IsProofOfStake = true }).CreateNewBlock(new Script());

                Block block = blockTemplate.Block;

                var stakeTxes = new List<StakeTx>();
                List<UnspentOutputReference> spendable = this.walletManager.GetSpendableTransactionsInWallet(walletSecret.WalletName, 1);

                FetchCoinsResponse coinset = this.coinView.FetchCoinsAsync(spendable.Select(t => t.Transaction.Id).ToArray()).GetAwaiter().GetResult();

                foreach (UnspentOutputReference infoTransaction in spendable)
                {
                    UnspentOutputs set = coinset.UnspentOutputs.FirstOrDefault(f => f?.TransactionId == infoTransaction.Transaction.Id);
                    TxOut utxo = (set != null) && (infoTransaction.Transaction.Index < set._Outputs.Length) ? set._Outputs[infoTransaction.Transaction.Index] : null;

                    if ((utxo != null) && (utxo.Value > Money.Zero))
                    {
                        var stakeTx = new StakeTx();

                        stakeTx.TxOut = utxo;
                        stakeTx.OutPoint = new OutPoint(set.TransactionId, infoTransaction.Transaction.Index);
                        stakeTx.Address = infoTransaction.Address;
                        stakeTx.OutputIndex = infoTransaction.Transaction.Index;
                        stakeTx.HashBlock = this.chain.GetBlock((int)set.Height).HashBlock;
                        stakeTx.UtxoSet = set;
                        stakeTx.Secret = walletSecret; // Temporary.
                        stakeTxes.Add(stakeTx);
                        this.logger.LogTrace("UTXO '{0}/{1}' with value {2} might be available for staking.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, utxo.Value);
                    }
                }

                // Trying to sign a block.
                if (this.StakeAndSignBlock(stakeTxes, block, prevBlock, blockTemplate.TotalFee, coinstakeTimestamp))
                {
                    this.logger.LogTrace("POS block signed successfully.");
                    var blockResult = new BlockResult { Block = block };
                    this.CheckStake(new ContextInformation(blockResult, this.network.Consensus), prevBlock, chainTip);

                    blockTemplate = null;
                }
                else
                {
                    this.logger.LogTrace("{0} failed, waiting {1} ms for next round...", nameof(this.StakeAndSignBlock), this.minerSleep);
                    Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }
            }
        }

        private void CheckStake(ContextInformation context, ChainedBlock prevBlock, ChainedBlock chainTip)
        {
            this.logger.LogTrace("({0}:'{1}/{2}',{3}:'{4}/{5}')", nameof(prevBlock), prevBlock.HashBlock, prevBlock.Height, nameof(chainTip), chainTip.HashBlock, chainTip.Height);

            Block block = context.BlockResult.Block;

            if (!BlockStake.IsProofOfStake(block))
            {
                this.logger.LogTrace("(-)[NOT_POS]");
                return;
            }

            // Verify hash target and signature of coinstake tx.
            BlockStake prevBlockStake = this.stakeChain.Get(prevBlock.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[NO_PREV_STAKE]");
                ConsensusErrors.PrevStakeNull.Throw();
            }

            context.SetStake();
            this.posConsensusValidator.StakeValidator.CheckProofOfStake(context, prevBlock, prevBlockStake, block.Transactions[1], block.Header.Bits.ToCompact());

            // The following is wrong, this should be REORG not SOLUTION FOUND
            // but this would just narrow the race condition in case of reorg, not mitigate it.
            // --------
            // Found a solution.
            // if (block.Header.HashPrevBlock != chainTip.HashBlock)
            // {
            //     this.logger.LogTrace("(-)[SOLUTION_FOUND]");
            //     return;
            // }

            // Validate the block.
            this.consensusLoop.AcceptBlock(context);

            if (context.BlockResult.ChainedBlock == null)
            {
                this.logger.LogTrace("(-)[REORG-2]");
                return;
            }

            if (context.BlockResult.Error != null)
            {
                this.logger.LogTrace("(-)[ACCEPT_BLOCK_ERROR]");
                return;
            }

            if (context.BlockResult.ChainedBlock.ChainWork <= chainTip.ChainWork)
            {
                this.logger.LogTrace("Chain tip's work is '{0}', newly minted block's work is only '{1}'.", context.BlockResult.ChainedBlock.ChainWork, chainTip.ChainWork);
                this.logger.LogTrace("(-)[LOW_CHAIN_WORK]");
                return;
            }

            // Similar logic to what's in the full node code.
            this.chain.SetTip(context.BlockResult.ChainedBlock);
            this.consensusLoop.Puller.SetLocation(this.consensusLoop.Tip);
            this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
            this.blockRepository.PutAsync(context.BlockResult.ChainedBlock.HashBlock, new List<Block> { block }).GetAwaiter().GetResult();
            this.signals.SignalBlock(block);

            this.logger.LogInformation("==================================================================");
            this.logger.LogInformation("Found new POS block hash '{0}' at height {1}.", context.BlockResult.ChainedBlock.HashBlock, context.BlockResult.ChainedBlock.Height);
            this.logger.LogInformation("==================================================================");

            // Wait for peers to get the block.
            this.logger.LogTrace("Waiting 1000 ms for newly minted block propagation...");
            Thread.Sleep(1000);

            // Ask peers for their headers.
            foreach (Node node in this.connection.ConnectedNodes)
            {
                this.logger.LogTrace("Updating headers of peer '{0}'.", node.RemoteSocketEndpoint);
                node.Behavior<ChainHeadersBehavior>().TrySync();
            }

            // Wait for all peers to accept the block.
            this.logger.LogTrace("Waiting up to 100 seconds for peers to accept the new block...");
            int retry = 0;
            foreach (Node node in this.connection.ConnectedNodes)
            {
                this.logger.LogTrace("Waiting for peer '{0}' to accept the new block...", node.RemoteSocketEndpoint);
                ChainHeadersBehavior chainBehaviour = node.Behavior<ChainHeadersBehavior>();
                while ((++retry < 100) && (chainBehaviour.PendingTip != this.chain.Tip))
                {
                    this.logger.LogTrace("Peer '{0}' still has different tip ('{1}/{2}'), waiting 1000 ms...", node.RemoteSocketEndpoint, chainBehaviour.PendingTip.HashBlock, chainBehaviour.PendingTip.Height);
                    Thread.Sleep(1000);
                }
            }

            if (retry == 100)
            {
                // Seems the block was not accepted.
                this.logger.LogTrace("Our newly minted block was rejected by peers.");
                throw new MinerException("Block rejected by peers");
            }
        }

        /// <summary>
        /// Attempts to find a POS staking solution and if it succeeds, then it completes a block
        /// to be mined and signes it.
        /// </summary>
        /// <param name="stakeTxes">List of coins that are available in the wallet for staking.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="chainTip">Tip of the best chain.</param>
        /// <param name="fees">Transaction fees from the transactions included in the block if we mine it.</param>
        /// <param name="coinstakeTimestamp">Maximal timestamp of the coinstake transaction. The actual timestamp can be lower, but not higher.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        private bool StakeAndSignBlock(List<StakeTx> stakeTxes, Block block, ChainedBlock chainTip, long fees, uint coinstakeTimestamp)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}/{5}',{6}:{7},{8}:{9})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(chainTip), chainTip.HashBlock, chainTip.Height, nameof(fees), fees, nameof(coinstakeTimestamp), coinstakeTimestamp);

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

            Key key = null;
            Transaction txCoinStake = new Transaction();
            txCoinStake.Time = coinstakeTimestamp;

            // Search to current coinstake time.
            long searchTime = txCoinStake.Time;

            long searchInterval = searchTime - this.lastCoinStakeSearchTime;
            this.lastCoinStakeSearchTime = searchTime;
            this.logger.LogTrace("Search interval set to {0}, last coinstake search timestamp set to {1}.", searchInterval, this.lastCoinStakeSearchTime);

            if (this.CreateCoinStake(stakeTxes, block, chainTip, searchInterval, fees, ref txCoinStake, ref key))
            {
                uint minTimestamp = BlockValidator.GetPastTimeLimit(chainTip) + 1;
                if (txCoinStake.Time >= minTimestamp)
                {
                    // Make sure coinstake would meet timestamp protocol
                    // as it would be the same as the block timestamp.
                    block.Transactions[0].Time = block.Header.Time = txCoinStake.Time;

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

                    block.Transactions.Insert(1, txCoinStake);
                    block.UpdateMerkleRoot();

                    // Append a signature to our block.
                    ECDSASignature signature = key.Sign(block.GetHash());

                    block.BlockSignatur = new BlockSignature { Signature = signature.ToDER() };
                    this.logger.LogTrace("(-):true");
                    return true;
                }
                else this.logger.LogTrace("Coinstake transaction created with too early timestamp {0}, minimal timestamp is {1}.", txCoinStake.Time, minTimestamp);
            }
            else this.logger.LogTrace("Unable to create coinstake transaction.");

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Creates a coinstake transaction with kernel that satisfies POS staking target. 
        /// </summary>
        /// <param name="stakeTxes">List of coins that are available in the wallet for staking.</param>
        /// <param name="chainTip">Tip of the best chain.</param>
        /// <param name="block">Template of the block that we are trying to mine.</param>
        /// <param name="searchInterval">Length of an unexplored block time space in seconds. It only makes sense to look for a solution within this interval.</param>
        /// <param name="fees">Transaction fees from the transactions included in the block if we mine it.</param>
        /// <param name="coinstakeTx">Coinstake transaction being constructed.</param>
        /// <param name="key">If the function succeeds, this is filled with private key for signing the coinstake kernel.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> otherwise.</returns>
        public bool CreateCoinStake(List<StakeTx> stakeTxes, Block block, ChainedBlock chainTip, long searchInterval, long fees, ref Transaction coinstakeTx, ref Key key)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}/{5}',{6}:{7},{8}:{9})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(chainTip), chainTip.HashBlock, chainTip.Height, nameof(searchInterval), searchInterval, nameof(fees), fees);

            coinstakeTx.Inputs.Clear();
            coinstakeTx.Outputs.Clear();

            // Mark coinstake transaction.
            coinstakeTx.Outputs.Add(new TxOut(Money.Zero, new Script()));

            long balance = this.GetBalance(stakeTxes).Satoshi;
            if (balance <= this.reserveBalance)
            {
                this.logger.LogTrace("Total balance of available UTXOs is {0}, which is lower than reserve balance {1}.", balance, this.reserveBalance);
                this.logger.LogTrace("(-)[BELOW_RESERVE]:false");
                return false;
            }

            // Select coins with suitable depth.
            List<StakeTx> setCoins = this.FindCoinsForStaking(stakeTxes, coinstakeTx.Time, balance - this.reserveBalance);
            if (!setCoins.Any())
            {
                this.logger.LogTrace("(-)[NO_SELECTION]:false");
                return false;
            }

            long ourWeight = setCoins.Sum(s => s.TxOut.Value);
            long networkWeight = (long)this.GetNetworkWeight();
            long expectedTime = StakeValidator.GetTargetSpacing(chainTip.Height) * networkWeight / ourWeight;
            decimal ourPercent = networkWeight != 0 ? 100.0m * (decimal)ourWeight / (decimal)networkWeight : 0;
            
            this.logger.LogInformation("Node staking with {0} ({1:0.00} % of the network weight {2}), est. time to find new block is {3}.", new Money(ourWeight), ourPercent, new Money(networkWeight), TimeSpan.FromSeconds(expectedTime));

            long minimalAllowedTime = chainTip.Header.Time + 1;
            this.logger.LogTrace("Trying to find staking solution among {0} transactions, minimal allowed time is {1}, coinstake time is {2}.", setCoins.Count, minimalAllowedTime, coinstakeTx.Time);

            // If the time after applying the mask is lower than minimal allowed time,
            // it is simply too early for us to mine, there can't be any valid solution.
            if ((coinstakeTx.Time & ~PosConsensusValidator.StakeTimestampMask) < minimalAllowedTime)
            {
                this.logger.LogTrace("(-)[TOO_EARLY_TIME_AFTER_LAST_BLOCK]:false");
                return false;
            }

            // Sort coins by amount, so that highest amounts are tried first
            // because they have greater chance to succeed and thus saving some work.
            setCoins = setCoins.OrderByDescending(o => o.TxOut.Value).ToList();

            // Inputs to coinstake transaction.
            // First we are looking for the input that will meet the POS target with its hash.
            // Once this is done we try to add additional small inputs that we can sign with the same key
            // in order to reduce the number of UTXOs.
            List<StakeTx> coinstakeInputs = new List<StakeTx>();

            // Script of the first coinstake input.
            Script scriptPubKeyKernel = null;

            // Total amount of input values in coinstake transaction.
            long coinstakeInputsValue = 0;

            foreach (StakeTx coin in setCoins)
            {
                this.logger.LogTrace("Trying UTXO from address '{0}', output amount {1}...", coin.Address.Address, coin.TxOut.Value);
                bool fKernelFound = false;

                scriptPubKeyKernel = coin.TxOut.ScriptPubKey;
                if (!PayToPubkeyTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel)
                    && !PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                {
                    this.logger.LogTrace("Kernel type must be P2PK or P2PKH, kernel rejected.");
                    continue;
                }

                for (uint n = 0; (n < searchInterval) && !fKernelFound; n++)
                {
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        this.logger.LogTrace("(-)[SHUTDOWN]:false");
                        return false;
                    }

                    uint txTime = coinstakeTx.Time - n;

                    // Once we reach previous block time + 1, we can't go any lower
                    // because it is required that the block time is greater than the previous block time.
                    if (txTime < minimalAllowedTime)
                        break;

                    if ((txTime & PosConsensusValidator.StakeTimestampMask) != 0)
                        continue;

                    if (chainTip != this.chain.Tip)
                    {
                        this.logger.LogTrace("(-)[REORG]:false");
                        return false;
                    }

                    this.logger.LogTrace("Trying with transaction time {0}...", txTime);
                    try
                    {
                        var prevoutStake = new OutPoint(coin.UtxoSet.TransactionId, coin.OutputIndex);
                        long nBlockTime = 0;

                        var context = new ContextInformation(new BlockResult { Block = block }, this.network.Consensus);
                        context.SetStake();
                        this.posConsensusValidator.StakeValidator.CheckKernel(context, chainTip, block.Header.Bits, txTime, prevoutStake, ref nBlockTime);

                        this.logger.LogTrace("Kernel found with solution hash '{0}'.", context.Stake.HashProofOfStake);

                        BitcoinAddress outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
                        Wallet.Wallet wallet = this.walletManager.GetWalletByName(coin.Secret.WalletName);
                        key = wallet.GetExtendedPrivateKeyForAddress(coin.Secret.WalletPassword, coin.Address).PrivateKey;

                        // Create a pubkey script form the current script.
                        Script scriptPubKeyOut = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(key.PubKey); // scriptPubKeyKernel

                        coin.Key = key;
                        coinstakeTx.Time = txTime;
                        coinstakeTx.AddInput(new TxIn(prevoutStake));
                        coinstakeInputsValue = coin.TxOut.Value;
                        coinstakeInputs.Add(coin);
                        coinstakeTx.Outputs.Add(new TxOut(0, scriptPubKeyOut));

                        this.logger.LogTrace("Kernel accepted.");
                        fKernelFound = true;
                        break;
                    }
                    catch (ConsensusErrorException cex)
                    {
                        this.logger.LogTrace("Checking kernel failed with exception: {0}.", cex.Message);
                        if (cex.ConsensusError == ConsensusErrors.StakeHashInvalidTarget)
                            continue;

                        throw;
                    }
                }

                // If kernel is found stop searching.
                if (fKernelFound)
                    break;
            }

            if (coinstakeInputsValue == 0)
            {
                this.logger.LogTrace("(-)[KERNEL_NOT_FOUND]:false");
                return false;
            }

            this.logger.LogTrace("Trying to reduce our staking UTXO set by adding additional inputs to coinstake transaction...");
            foreach (StakeTx stakeTx in setCoins)
            {
                // Attempt to add more inputs.
                // Only add coins of the same key/address as kernel.
                if ((coinstakeTx.Outputs.Count == 2)
                    && ((stakeTx.TxOut.ScriptPubKey == scriptPubKeyKernel) || (stakeTx.TxOut.ScriptPubKey == coinstakeTx.Outputs[1].ScriptPubKey))
                    && (stakeTx.UtxoSet.TransactionId != coinstakeTx.Inputs[0].PrevOut.Hash))
                {
                    this.logger.LogTrace("Found candidate UTXO '{0}/{1}' with {2} coins.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, stakeTx.TxOut.Value);

                    long timeWeight = BlockValidator.GetWeight((long)stakeTx.UtxoSet.Time, (long)coinstakeTx.Time);

                    // Don't add inputs that would violate the reserve limit.
                    if ((coinstakeInputsValue + stakeTx.TxOut.Value) > (balance - this.reserveBalance))
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' rejected because if it was added, we would not have required reserve anymore. Its value is {2}, we already used {3}, our total balance is {4} and reserve is {5}.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, stakeTx.TxOut.Value, coinstakeInputsValue, balance, this.reserveBalance);
                        continue;
                    }

                    // Do not add additional significant input.
                    if (stakeTx.TxOut.Value >= GetStakeCombineThreshold())
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' rejected because its value is too big.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N);
                        continue;
                    }

                    // Do not add input that is still too young.
                    // V3 case is properly handled by selection function.
                    if (!BlockValidator.IsProtocolV3((int)coinstakeTx.Time))
                    {
                        if (timeWeight < BlockValidator.StakeMinAge)
                            continue;
                    }

                    coinstakeTx.Inputs.Add(new TxIn(new OutPoint(stakeTx.UtxoSet.TransactionId, stakeTx.OutputIndex)));

                    coinstakeInputsValue += stakeTx.TxOut.Value;
                    coinstakeInputs.Add(stakeTx);

                    this.logger.LogTrace("UTXO '{0}/{1}' joined to coinstake transaction.");

                    // Stop adding more inputs if already too many inputs.
                    if (coinstakeTx.Inputs.Count >= 100)
                    {
                        this.logger.LogTrace("Number of coinstake inputs reached the limit of 100.");
                        break;
                    }
                }
            }

            // Calculate coin age reward.
            if (!this.posConsensusValidator.StakeValidator.GetCoinAge(this.chain, this.coinView, coinstakeTx, chainTip, out ulong coinAge))
            {
                this.logger.LogTrace("(-)[AGE_CALCULATION_FAILED]:false");
                return false;
            }

            long reward = fees + this.posConsensusValidator.GetProofOfStakeReward(chainTip.Height);
            if (reward <= 0)
            {
                // TODO: This can't happen unless we remove reward for mined block.
                // If this can happen over time then this check could be done much sooner
                // to avoid a lot of computation.
                this.logger.LogTrace("(-)[NO_REWARD]:false");
                return false;
            }

            coinstakeInputsValue += reward;

            // Split stake if above threshold.
            if (coinstakeInputsValue >= GetStakeSplitThreshold())
            {
                this.logger.LogTrace("Coinstake UTXO will be split to two.");
                coinstakeTx.Outputs.Add(new TxOut(0, coinstakeTx.Outputs[1].ScriptPubKey));
            }

            // Set output amount.
            if (coinstakeTx.Outputs.Count == 3)
            {
                coinstakeTx.Outputs[1].Value = (coinstakeInputsValue / 2 / BlockValidator.CENT) * BlockValidator.CENT;
                coinstakeTx.Outputs[2].Value = coinstakeInputsValue - coinstakeTx.Outputs[1].Value;
                this.logger.LogTrace("Coinstake first output value is {0}, second is {2}.", coinstakeTx.Outputs[1].Value, coinstakeTx.Outputs[2].Value);
            }
            else
            {
                coinstakeTx.Outputs[1].Value = coinstakeInputsValue;
                this.logger.LogTrace("Coinstake output value is {0}.", coinstakeTx.Outputs[1].Value);
            }

            // Sign.
            foreach (StakeTx walletTx in coinstakeInputs)
            {
                if (!this.SignSignature(walletTx, coinstakeTx))
                {
                    this.logger.LogTrace("(-)[SIGN_FAILED]:false");
                    return false;
                }
            }

            // Limit size.
            int serializedSize = coinstakeTx.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION, SerializationType.Network);
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

        private bool SignSignature(StakeTx from, Transaction txTo, params Script[] knownRedeems)
        {
            this.logger.LogTrace("({0}:'{1}/{2}')", nameof(from), from.OutPoint.Hash, from.OutPoint.N);
            try
            {
                new TransactionBuilder()
                    .AddKeys(from.Key)
                    .AddKnownRedeems(knownRedeems)
                    .AddCoins(new Coin(from.OutPoint, from.TxOut))
                    .SignTransactionInPlace(txTo);
            }
            catch (Exception)
            {
                this.logger.LogTrace("(-):false");
                return false;
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        private static long GetStakeCombineThreshold()
        {
            return 100 * BlockValidator.COIN;
        }

        private static long GetStakeSplitThreshold()
        {
            return 2 * GetStakeCombineThreshold();
        }

        public Money GetBalance(List<StakeTx> stakeTxes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count);

            var money = new Money(0);
            foreach (StakeTx stakeTx in stakeTxes)
            {
                // Must wait until coinbase is safely deep enough in the chain before valuing it.
                if ((stakeTx.UtxoSet.IsCoinbase || stakeTx.UtxoSet.IsCoinstake) && (this.GetBlocksToMaturity(stakeTx) > 0))
                    continue;

                money += stakeTx.TxOut.Value;
            }

            this.logger.LogTrace("(-):{0}", money);
            return money;
        }

        /// <summary>
        /// Selects coins that are suitable for staking. 
        /// <para>
        /// Such a coin has to be confirmed with enough confirmations - i.e. has suitable depth,
        /// and it also has to be mature and meet requirement for minimal value.
        /// </para>
        /// </summary>
        /// <param name="stakeTxes">List of coins that are candidates for being used for staking.</param>
        /// <param name="spendTime">Timestamp of the coinstake transaction.</param>
        /// <param name="maxValue">Maximal amount of coins that can be used for staking.</param>
        /// <returns>List of coins that meet the requirements.</returns>
        private List<StakeTx> FindCoinsForStaking(List<StakeTx> stakeTxes, uint spendTime, long maxValue)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4},{5}:{6})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(spendTime), spendTime, nameof(maxValue), maxValue);
            var res = new List<StakeTx>();

            long requiredDepth = this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations;
            foreach (StakeTx stakeTx in stakeTxes)
            {
                int depth = this.GetDepthInMainChain(stakeTx);
                this.logger.LogTrace("Checking if UTXO '{0}/{1}' value {2} can be added, its depth is {3}.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, stakeTx.TxOut.Value, depth);

                if (depth < 1)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' is new or reorg happened.");
                    continue;
                }

                if (BlockValidator.IsProtocolV3((int)spendTime))
                {
                    if (depth < requiredDepth)
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' depth {2} is lower than required minimum depth {3}.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, depth, requiredDepth);
                        continue;
                    }
                }
                else
                {
                    // Filtering by tx timestamp instead of block timestamp may give false positives but never false negatives.
                    if (stakeTx.UtxoSet.Time + this.network.Consensus.Option<PosConsensusOptions>().StakeMinAge > spendTime)
                        continue;
                }

                if (this.GetBlocksToMaturity(stakeTx) > 0)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' can't be added because it is not mature.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N);
                    continue;
                }

                if (stakeTx.TxOut.Value < this.minimumInputValue)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' can't be added because its value {2} is lower than required minimal value {3}.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, stakeTx.TxOut.Value, this.minimumInputValue);
                    continue;
                }

                if (stakeTx.TxOut.Value > maxValue)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' can't be added because its value {2} is greater than required maximal value {3}.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, stakeTx.TxOut.Value, maxValue);
                    continue;
                }

                this.logger.LogTrace("UTXO '{0}/{1}' accepted.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N);
                res.Add(stakeTx);
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(res.Count), res.Count);
            return res;
        }

        private int GetBlocksToMaturity(StakeTx stakeTx)
        {
            if (!(stakeTx.UtxoSet.IsCoinbase || stakeTx.UtxoSet.IsCoinstake))
                return 0;

            return Math.Max(0, (int)this.network.Consensus.Option<PosConsensusOptions>().COINBASE_MATURITY + 1 - this.GetDepthInMainChain(stakeTx));
        }

        // Return depth of transaction in blockchain:
        // -1  : not in blockchain, and not in memory pool (conflicted transaction),
        //  0  : in memory pool, waiting to be included in a block,
        // >=1 : this many blocks deep in the main chain.
        private int GetDepthInMainChain(StakeTx stakeTx)
        {
            ChainedBlock chainedBlock = this.chain.GetBlock(stakeTx.HashBlock);

            if (chainedBlock == null)
                return -1;

            // TODO: Check if in memory pool then return 0.
            return this.chain.Tip.Height - chainedBlock.Height + 1;
        }

        /// <summary>
        /// Calculates staking difficulty for a specific block.
        /// </summary>
        /// <param name="block">Block at which to calculate the difficulty.</param>
        /// <returns>Staking difficulty.</returns>
        /// <remarks>
        /// The actual idea behind the calculation is a mystery. It was simply ported from 
        /// https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L16 .
        /// </remarks>
        public double GetDifficulty(ChainedBlock block)
        {
            this.logger.LogTrace("({0}:'{1}/{2}')", nameof(block), block.HashBlock, block.Height);

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

                block = StakeValidator.GetLastBlockIndex(this.stakeChain, tip, false);
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

        /// <summary>
        /// Estimates the total staking weight of the network.
        /// </summary>
        /// <returns>Estimated number of coins that are used by all stakers on the network.</returns>
        /// <remarks>
        /// The idea behind estimating the network staking weight is very similar to estimating 
        /// the total hash power of PoW network. The difficulty retarget algorithm tries to make 
        /// sure of certain distribution of the blocks over a period of time. Base on real distribution
        /// and using the actual difficulty targets, one is able to compute how much stake was 
        /// presented on the network to generate each block.
        /// <para>
        /// The method was ported from 
        /// https://github.com/stratisproject/stratisX/blob/47851b7337f528f52ec20e86dca7dcead8191cf5/src/rpcblockchain.cpp#L74 .
        /// </para>
        /// </remarks>
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
    }
}
