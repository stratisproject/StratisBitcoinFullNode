using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using IBlockRepository = Stratis.Bitcoin.Features.BlockStore.IBlockRepository;
using NBitcoin.Crypto;

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

        private Task mining;
        private Money reserveBalance;
        private readonly int minimumInputValue;
        private readonly int minerSleep;

        private long lastCoinStakeSearchTime;
        private long lastCoinStakeSearchInterval;

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
            this.lastCoinStakeSearchTime = Utils.DateTimeToUnixTime(this.dateTimeProvider.GetTimeOffset()); // startup timestamp
            this.reserveBalance = 0; // TOOD:settings.ReserveBalance 
            this.minimumInputValue = 0;

            this.posConsensusValidator = consensusLoop.Validator as PosConsensusValidator;
        }

        public Task Mine(WalletSecret walletSecret)
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

                this.GenerateBlocks(walletSecret);

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
            this.lastCoinStakeSearchInterval = 0;

            BlockTemplate pblocktemplate = null;
            bool tryToSync = true;

            while (true)
            {
                if (this.chain.Tip != this.consensusLoop.Tip)
                {
                    this.logger.LogTrace("(-)[REORG]");
                    return;
                }

                while (!this.connection.ConnectedNodes.Any() || this.chainState.IsInitialBlockDownload)
                {
                    if (!this.connection.ConnectedNodes.Any()) this.logger.LogTrace("Waiting to be connected with at least one network peer...");
                    else this.logger.LogTrace("Waiting for IBD to complete...");

                    this.lastCoinStakeSearchInterval = 0;
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
                    bool fewPeers = this.connection.ConnectedNodes.Count() < 3;
                    bool lastBlockTooOld = this.chain.Tip.Header.Time < (this.dateTimeProvider.GetTime() - 10 * 60);
                    if (fewPeers || lastBlockTooOld)
                    {
                        if (fewPeers) this.logger.LogTrace("Node is connected to few peers.");
                        if (lastBlockTooOld) this.logger.LogTrace("Last block is too old, timestamp {0}.", this.chain.Tip.Header.Time);

                        Task.Delay(TimeSpan.FromMilliseconds(60000), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                        //this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(60000));
                        continue;
                    }
                }

                if (pblocktemplate == null)
                    pblocktemplate = this.blockAssemblerFactory.Create(new AssemblerOptions() { IsProofOfStake = true }).CreateNewBlock(new Script());

                Block pblock = pblocktemplate.Block;
                ChainedBlock pindexPrev = this.consensusLoop.Tip;

                var stakeTxes = new List<StakeTx>();
                List<UnspentOutputReference> spendable = this.walletManager.GetSpendableTransactionsInWallet(walletSecret.WalletName, 1);

                FetchCoinsResponse coinset = this.coinView.FetchCoinsAsync(spendable.Select(t => t.Transaction.Id).ToArray()).GetAwaiter().GetResult();

                foreach (UnspentOutputReference infoTransaction in spendable)
                {
                    UnspentOutputs set = coinset.UnspentOutputs.FirstOrDefault(f => f?.TransactionId == infoTransaction.Transaction.Id);
                    TxOut utxo = set?._Outputs[infoTransaction.Transaction.Index];

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
                if (this.SignBlock(stakeTxes, pblock, pindexPrev, pblocktemplate.TotalFee))
                {
                    this.logger.LogTrace("POS block signed successfully.");
                    var blockResult = new BlockResult { Block = pblock };
                    this.CheckStake(new ContextInformation(blockResult, this.network.Consensus), pindexPrev);

                    pblocktemplate = null;
                }
                else
                {
                    this.logger.LogTrace("{0} failed, waiting {1} ms for next round...", nameof(this.SignBlock), this.minerSleep);
                    Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }
            }
        }

        private void CheckStake(ContextInformation context, ChainedBlock pindexPrev)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(pindexPrev), pindexPrev?.HashBlock);

            Block block = context.BlockResult.Block;

            if (!BlockStake.IsProofOfStake(block))
            {
                this.logger.LogTrace("(-)[NOT_POS]");
                return;
            }

            // Verify hash target and signature of coinstake tx.
            BlockStake prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
            if (prevBlockStake == null)
            {
                this.logger.LogTrace("(-)[NO_PREV_STAKE]");
                ConsensusErrors.PrevStakeNull.Throw();
            }

            context.SetStake();
            this.posConsensusValidator.StakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], block.Header.Bits.ToCompact());

            // Found a solution.
            if (block.Header.HashPrevBlock != this.chain.Tip.HashBlock)
            {
                this.logger.LogTrace("(-)[REORG]");
                return;
            }
            
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

            if (context.BlockResult.ChainedBlock.ChainWork <= this.chain.Tip.ChainWork)
            {
                this.logger.LogTrace("Chain tip's work is '{0}', newly minted block's work is only '{1}'.", context.BlockResult.ChainedBlock.ChainWork, this.chain.Tip.ChainWork);
                this.logger.LogTrace("(-)[CHAIN_WORK]");
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

        private bool SignBlock(List<StakeTx> stakeTxes, Block block, ChainedBlock pindexBest, long fees)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}/{5}',{6}:{7})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(pindexBest), pindexBest.HashBlock, pindexBest.Height, nameof(fees), fees);

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

            txCoinStake.Time &= ~PosConsensusValidator.StakeTimestampMask;

            // Search to current time.
            long searchTime = txCoinStake.Time;

            if (searchTime > this.lastCoinStakeSearchTime)
            {
                long searchInterval = searchTime - this.lastCoinStakeSearchTime;
                if (this.CreateCoinStake(stakeTxes, pindexBest, block, searchInterval, fees, ref txCoinStake, ref key))
                {
                    uint minTimestamp = BlockValidator.GetPastTimeLimit(pindexBest) + 1;
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

                this.lastCoinStakeSearchInterval = searchTime - this.lastCoinStakeSearchTime;
                this.lastCoinStakeSearchTime = searchTime;
                this.logger.LogTrace("Last coinstake search interval set to {0}, last coinstake search timestamp set to {1}.", this.lastCoinStakeSearchInterval, this.lastCoinStakeSearchTime);
            }
            else this.logger.LogTrace("Current coinstake time {0} is not greater than last search timestamp {1}.", searchTime, this.lastCoinStakeSearchTime);

            this.logger.LogTrace("(-):false");
            return false;
        }

        public bool CreateCoinStake(List<StakeTx> stakeTxes, ChainedBlock pindexBest, Block block, long nSearchInterval,
            long fees, ref Transaction txNew, ref Key key)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}/{5}',{6}:{7},{8}:{9})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(pindexBest), pindexBest.HashBlock, pindexBest.Height, nameof(nSearchInterval), nSearchInterval, nameof(fees), fees);

            ChainedBlock pindexPrev = pindexBest;
            var bnTargetPerCoinDay = new Target(block.Header.Bits).ToCompact();

            txNew.Inputs.Clear();
            txNew.Outputs.Clear();

            // Mark coinstake transaction.
            txNew.Outputs.Add(new TxOut(Money.Zero, new Script()));

            // Choose coins to use.
            long nBalance = this.GetBalance(stakeTxes).Satoshi;

            if (nBalance <= this.reserveBalance)
            {
                this.logger.LogTrace("Total balance of available UTXOs is {0}, which is lower than reserve balance {1}.", nBalance, this.reserveBalance);
                this.logger.LogTrace("(-)[BELOW_RESERVE]:false");
                return false;
            }

            List<StakeTx> vwtxPrev = new List<StakeTx>();

            List<StakeTx> setCoins;
            long nValueIn = 0;

            // Select coins with suitable depth.
            if (!this.SelectCoinsForStaking(stakeTxes, nBalance - this.reserveBalance, txNew.Time, out setCoins, out nValueIn))
            {
                this.logger.LogTrace("(-)[SELECTION_FAILED]:false");
                return false;
            }

            if (!setCoins.Any())
            {
                this.logger.LogTrace("(-)[NO_SELECTION]:false");
                return false;
            }

            // Replace this with staking weight.
            this.logger.LogInformation("Node staking with amount {0}.", new Money(setCoins.Sum(s => s.TxOut.Value))); 

            long nCredit = 0;
            Script scriptPubKeyKernel = null;
            
            // Note: I would expect to see coins sorted by weight on the original implementation
            // it sorts the coins from heighest weight.
            setCoins = setCoins.OrderByDescending(o => o.TxOut.Value).ToList();

            this.logger.LogTrace("Trying to find staking solution among {0} transactions.", setCoins.Count);
            foreach (StakeTx coin in setCoins)
            {
                this.logger.LogTrace("Trying UTXO from address '{0}', output amount {1}...", coin.Address.Address, coin.TxOut.Value);
                int maxStakeSearchInterval = 60;
                bool fKernelFound = false;

                for (uint n = 0; n < Math.Min(nSearchInterval, maxStakeSearchInterval) && !fKernelFound; n++)
                {
                    if (pindexPrev != this.chain.Tip)
                    {
                        this.logger.LogTrace("(-)[REORG]:false");
                        return false;
                    }

                    uint txTime = txNew.Time - n;
                    this.logger.LogTrace("Trying with transaction time {0}...", txTime);
                    try
                    {
                        var prevoutStake = new OutPoint(coin.UtxoSet.TransactionId, coin.OutputIndex);
                        long nBlockTime = 0;

                        var context = new ContextInformation(new BlockResult { Block = block }, this.network.Consensus);
                        context.SetStake();
                        this.posConsensusValidator.StakeValidator.CheckKernel(context, pindexPrev, block.Header.Bits, txTime, prevoutStake, ref nBlockTime);

                        // TODO: Do this check as a first thing in the loop, not after checking kernel.
                        if ((txTime & PosConsensusValidator.StakeTimestampMask) != 0)
                        {
                            this.logger.LogTrace("Trying with transaction time {0}...", txTime);
                            continue;
                        }

                        // TODO: This is always true - CheckKernel either throws or fills in POS hash.
                        if (context.Stake.HashProofOfStake != null)
                        {
                            this.logger.LogTrace("Kernel found with solution hash '{0}'.", context.Stake.HashProofOfStake);
                            scriptPubKeyKernel = coin.TxOut.ScriptPubKey;

                            key = null;
                            // Calculate the key type.
                            // TODO: Why there are two if blocks with same body?
                            // Can't we simply make OR condition with one block?
                            // Also these checks could probably precede CheckKernel call as it does not affact TxOut.ScriptPubKey.
                            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                            {
                                BitcoinAddress outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
                                Wallet.Wallet wallet = this.walletManager.GetWalletByName(coin.Secret.WalletName);
                                key = wallet.GetExtendedPrivateKeyForAddress(coin.Secret.WalletPassword, coin.Address).PrivateKey;
                            }
                            else if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                            {
                                BitcoinAddress outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
                                Wallet.Wallet wallet = this.walletManager.GetWalletByName(coin.Secret.WalletName);
                                key = wallet.GetExtendedPrivateKeyForAddress(coin.Secret.WalletPassword, coin.Address).PrivateKey;
                            }
                            else
                            {
                                this.logger.LogTrace("Kernel type must be P2PK or P2PKH, kernel rejected.");
                                break;
                            }

                            // Create a pubkey script form the current script.
                            Script scriptPubKeyOut = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(key.PubKey); // scriptPubKeyKernel

                            coin.Key = key;
                            txNew.Time = txTime;
                            txNew.AddInput(new TxIn(prevoutStake));
                            nCredit += coin.TxOut.Value;
                            vwtxPrev.Add(coin);
                            txNew.Outputs.Add(new TxOut(0, scriptPubKeyOut));

                            this.logger.LogTrace("Kernel accepted.");
                            fKernelFound = true;
                            break;
                        }
                        else this.logger.LogTrace("Kernel found, but no POS hash provided.");
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

            if (nCredit == 0)
            {
                this.logger.LogTrace("(-)[KERNEL_NOT_FOUND]:false");
                return false;
            }

            // TODO: This seems to be wrong because we found kernel
            // for a particular UTXO and now if we find out that its 
            // value is too big, such that we would not have our reserve
            // if we use it, we end the whole process completely here,
            // but this should disqualify only the particular transaction
            // there could be other transactions we have not even tried
            // and they could satisfy the target and have lower amount
            // to satisfy the reserve condition.
            //
            // Maybe it shouldn't even occur as SelectCoinsForStaking 
            // should avoid selecting coins that would violate the reserve.
            // But that does not currently happen.
            if (nCredit > (nBalance - this.reserveBalance))
            {
                this.logger.LogTrace("(-)[RESERVE]:false");
                return false;
            }

            this.logger.LogTrace("Trying to reduce our staking UTXO set by adding additional inputs to coinstake transaction...");
            foreach (StakeTx coin in setCoins)
            {
                StakeTx cointrx = coin;

                // Attempt to add more inputs.
                // Only add coins of the same key/address as kernel.
                if ((txNew.Outputs.Count == 2)
                    && ((cointrx.TxOut.ScriptPubKey == scriptPubKeyKernel) || (cointrx.TxOut.ScriptPubKey == txNew.Outputs[1].ScriptPubKey))
                    && (cointrx.UtxoSet.TransactionId != txNew.Inputs[0].PrevOut.Hash))
                {
                    this.logger.LogTrace("Found candidate UTXO '{0}/{1}' with {2} coins.", cointrx.OutPoint.Hash, cointrx.OutPoint.N, cointrx.TxOut.Value);

                    long nTimeWeight = BlockValidator.GetWeight((long)cointrx.UtxoSet.Time, (long)txNew.Time);

                    // Stop adding more inputs if already too many inputs.
                    // TODO: This should rather be after txNew.Inputs.Add() below.
                    if (txNew.Inputs.Count >= 100)
                    {
                        this.logger.LogTrace("Number of coinstake inputs reached the limit of 100.");
                        break;
                    }

                    // Stop adding inputs if reached reserve limit.
                    // TODO: This should only disqualify this one coin, not all coins
                    // maybe there are other coins that could be joined with their inputs?
                    if ((nCredit + cointrx.TxOut.Value) > (nBalance - this.reserveBalance))
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' rejected because if it was added, we would not have required reserve anymore. Its value is {2}, we already used {3}, our total balance is {4} and reserve is {5}.", cointrx.OutPoint.Hash, cointrx.OutPoint.N, cointrx.TxOut.Value, nCredit, nBalance, this.reserveBalance);
                        break;
                    }

                    // Do not add additional significant input.
                    if (cointrx.TxOut.Value >= GetStakeCombineThreshold())
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' rejected because its value is too big.", cointrx.OutPoint.Hash, cointrx.OutPoint.N);
                        continue;
                    }

                    // Do not add input that is still too young.
                    if (BlockValidator.IsProtocolV3((int)txNew.Time))
                    {
                        // Properly handled by selection function.
                    }
                    else
                    {
                        if (nTimeWeight < BlockValidator.StakeMinAge)
                            continue;
                    }

                    txNew.Inputs.Add(new TxIn(new OutPoint(cointrx.UtxoSet.TransactionId, cointrx.OutputIndex)));

                    nCredit += cointrx.TxOut.Value;
                    vwtxPrev.Add(coin);

                    this.logger.LogTrace("UTXO '{0}/{1}' joined to coinstake transaction.");
                }
            }

            // Calculate coin age reward.
            // TODO: This is not used  anywhere, delete it?
            ulong nCoinAge;
            if (!this.posConsensusValidator.StakeValidator.GetCoinAge(this.chain, this.coinView, txNew, pindexPrev, out nCoinAge))
                return false; //error("CreateCoinStake : failed to calculate coin age");

            long nReward = fees + this.posConsensusValidator.GetProofOfStakeReward(pindexPrev.Height);
            if (nReward <= 0)
            {
                // TODO: This can't happen unless we remove reward for mined block.
                // If this can happen over time then this check could be done much sooner
                // to avoid a lot of computation.
                this.logger.LogTrace("(-)[NO_REWARD]:false");
                return false;
            }

            nCredit += nReward;

            // Split stake if above threshold.
            if (nCredit >= GetStakeSplitThreshold())
            {
                this.logger.LogTrace("Coinstake UTXO will be split to two.");
                txNew.Outputs.Add(new TxOut(0, txNew.Outputs[1].ScriptPubKey));
            }

            // Set output amount.
            if (txNew.Outputs.Count == 3)
            {
                txNew.Outputs[1].Value = (nCredit / 2 / BlockValidator.CENT) * BlockValidator.CENT;
                txNew.Outputs[2].Value = nCredit - txNew.Outputs[1].Value;
                this.logger.LogTrace("Coinstake first output value is {0}, second is {2}.", txNew.Outputs[1].Value, txNew.Outputs[2].Value);
            }
            else
            {
                txNew.Outputs[1].Value = nCredit;
                this.logger.LogTrace("Coinstake output value is {0}.", txNew.Outputs[1].Value);
            }

            // Sign.
            foreach (StakeTx walletTx in vwtxPrev)
            {
                if (!this.SignSignature(walletTx, txNew))
                {
                    this.logger.LogTrace("(-)[SIGN_FAILED]:false");
                    return false;
                }
            }

            // Limit size.
            int nBytes = txNew.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION, SerializationType.Network);
            if (nBytes >= MaxBlockSizeGen / 5)
            {
                this.logger.LogTrace("Coinstake size {0} bytes exceeded limit {1} bytes.", nBytes, MaxBlockSizeGen / 5);
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
                if ((stakeTx.UtxoSet.IsCoinbase || stakeTx.UtxoSet.IsCoinstake) && this.GetBlocksToMaturity(stakeTx) > 0)
                    continue;

                money += stakeTx.TxOut.Value;
            }

            this.logger.LogTrace("(-):{0}", money);
            return money;
        }

        private bool SelectCoinsForStaking(List<StakeTx> stakeTxes, long nTargetValue, uint nSpendTime, out List<StakeTx> setCoinsRet, out long nValueRet)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4},{5}:{6})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(nTargetValue), nTargetValue, nameof(nSpendTime), nSpendTime);

            List<StakeOutput> coins = this.AvailableCoinsForStaking(stakeTxes, nSpendTime);
            setCoinsRet = new List<StakeTx>();
            nValueRet = 0;

            foreach (StakeOutput output in coins)
            {
                this.logger.LogTrace("Checking if UTXO '{0}/{1}' value {2} can be added.", output.StakeTx.OutPoint.Hash, output.StakeTx.OutPoint.N, output.StakeTx.TxOut.Value);
                StakeTx pcoin = output.StakeTx;

                // Stop if we've chosen enough inputs.
                if (nValueRet >= nTargetValue)
                {
                    this.logger.LogTrace("Target amount reached, exiting loop.");
                    break;
                }

                Money n = pcoin.TxOut.Value;
                if (n >= nTargetValue)
                {
                    // If input value is greater or equal to target then simply insert
                    // it into the current subset and exit.
                    setCoinsRet.Add(pcoin);
                    nValueRet += n;
                    this.logger.LogTrace("UTXO '{0}/{1}' value {2} can be added and is large enough to reach the target.", output.StakeTx.OutPoint.Hash, output.StakeTx.OutPoint.N, output.StakeTx.TxOut.Value);
                    break;
                }
                else if (n < (nTargetValue + BlockValidator.CENT))
                {
                    setCoinsRet.Add(pcoin);
                    nValueRet += n;
                    this.logger.LogTrace("UTXO '{0}/{1}' value {2} can be added, we now have {3}.", output.StakeTx.OutPoint.Hash, output.StakeTx.OutPoint.N, output.StakeTx.TxOut.Value, nValueRet);
                }
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        private List<StakeOutput> AvailableCoinsForStaking(List<StakeTx> stakeTxes, uint nSpendTime)
        {
            this.logger.LogTrace("({0}.{1}:{2},{3}:{4})", nameof(stakeTxes), nameof(stakeTxes.Count), stakeTxes.Count, nameof(nSpendTime), nSpendTime);
            var vCoins = new List<StakeOutput>();

            long requiredDepth = this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations;
            foreach (StakeTx pcoin in stakeTxes)
            {
                int nDepth = this.GetDepthInMainChain(pcoin);
                this.logger.LogTrace("Checking if UTXO '{0}/{1}' value {2} can be added, its depth is {3}.", pcoin.OutPoint.Hash, pcoin.OutPoint.N, pcoin.TxOut.Value, nDepth);

                if (nDepth < 1)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' is new or reorg happened.");
                    continue;
                }

                if (BlockValidator.IsProtocolV3((int)nSpendTime))
                {
                    if (nDepth < requiredDepth)
                    {
                        this.logger.LogTrace("UTXO '{0}/{1}' depth {2} is lower than required minimum depth {3}.", pcoin.OutPoint.Hash, pcoin.OutPoint.N, nDepth, requiredDepth);
                        continue;
                    }
                }
                else
                {
                    // Filtering by tx timestamp instead of block timestamp may give false positives but never false negatives.
                    if (pcoin.UtxoSet.Time + this.network.Consensus.Option<PosConsensusOptions>().StakeMinAge > nSpendTime)
                        continue;
                }

                if (this.GetBlocksToMaturity(pcoin) > 0)
                {
                    this.logger.LogTrace("UTXO '{0}/{1}' can't be added because it is not mature.", pcoin.OutPoint.Hash, pcoin.OutPoint.N);
                    continue;
                }

                if (pcoin.TxOut.Value >= this.minimumInputValue)
                {
                    // Check if the coin is already staking.
                    this.logger.LogTrace("UTXO '{0}/{1}' accepted.", pcoin.OutPoint.Hash, pcoin.OutPoint.N);
                    vCoins.Add(new StakeOutput { Depth = nDepth, StakeTx = pcoin });
                }
                else this.logger.LogTrace("UTXO '{0}/{1}' can't be added because its value {2} is below required minimum value {3}.", pcoin.OutPoint.Hash, pcoin.OutPoint.N, pcoin.TxOut.Value, this.minimumInputValue);
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(vCoins.Count), vCoins.Count);
            return vCoins;
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
    }
}
