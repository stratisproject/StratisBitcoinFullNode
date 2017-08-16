using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
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
        // Default for -blockmintxfee, which sets the minimum feerate for a transaction in blocks created by mining code 
        public const int DefaultBlockMinTxFee = 1000;
        // Default for -blockmaxsize, which controls the maximum size of block the mining code will create 
        public const int DefaultBlockMaxSize = 750000;
        // Default for -blockmaxweight, which controls the range of block weights the mining code will create 
        public const int DefaultBlockMaxWeight = 3000000;

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
        private readonly WalletManager wallet;
        private readonly PosConsensusValidator posConsensusValidator;
        private readonly ILogger logger;

        private Task mining;
        private readonly long lastCoinStakeSearchTime;
        private Money reserveBalance;
        private readonly int minimumInputValue;
        private readonly int minerSleep;

        
        public long LastCoinStakeSearchInterval;
        public long LastCoinStakeSearchTime;

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
            this.wallet = wallet as WalletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.minerSleep = 500; // GetArg("-minersleep", 500);
            this.lastCoinStakeSearchTime = Utils.DateTimeToUnixTime(this.dateTimeProvider.GetTimeOffset()); // startup timestamp
            this.reserveBalance = 0; // TOOD:settings.ReserveBalance 
            this.minimumInputValue = 0;

            this.posConsensusValidator = consensusLoop.Validator as PosConsensusValidator;
        }

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

        public Task Mine(WalletSecret walletSecret)
        {
            if (this.mining != null)
                return this.mining; // already mining

            this.mining = this.asyncLoopFactory.Run("PosMining.Mine", token =>
            {
                this.GenerateBlocks(walletSecret);
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMilliseconds(this.minerSleep),
            startAfter: TimeSpans.TenSeconds);

            return this.mining;
        }

        public void GenerateBlocks(WalletSecret walletSecret)
        {
            this.LastCoinStakeSearchInterval = 0;

            BlockTemplate pblocktemplate = null;
            bool tryToSync = true;

            while (true)
            {
                if (this.chain.Tip != this.consensusLoop.Tip)
                    return;

                while (!this.connection.ConnectedNodes.Any() || this.chainState.IsInitialBlockDownload)
                {
                    this.LastCoinStakeSearchInterval = 0;
                    tryToSync = true;
                    Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }

                if (tryToSync)
                {
                    tryToSync = false;
                    if (this.connection.ConnectedNodes.Count() < 3 ||
                        this.chain.Tip.Header.Time < this.dateTimeProvider.GetTime() - 10*60)
                    {
                        //this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(60000));
                        continue;
                    }
                }

                if (pblocktemplate == null)
                    pblocktemplate = this.blockAssemblerFactory.Create(new AssemblerOptions() {IsProofOfStake = true}).CreateNewBlock(new Script());


                var pblock = pblocktemplate.Block;
                var pindexPrev = this.consensusLoop.Tip;

                var stakeTxes = new List<StakeTx>();
                var spendable = this.wallet.GetSpendableTransactions(walletSecret.WalletName, 1);

                var coinset = this.coinView.FetchCoinsAsync(spendable.SelectMany(s => s.UnspentOutputs.Select(t => t.Transaction.Id)).ToArray()).GetAwaiter().GetResult();

                foreach (var unspentInfo in spendable)
                {
                    foreach (var infoTransaction in unspentInfo.UnspentOutputs)
                    {
                        var set = coinset.UnspentOutputs.FirstOrDefault(f => f?.TransactionId == infoTransaction.Transaction.Id);
                        var utxo = set?._Outputs[infoTransaction.Transaction.Index];

                        if (utxo != null && utxo.Value > Money.Zero)
                        {
                            var stakeTx = new StakeTx();

                            stakeTx.TxOut = utxo;
                            stakeTx.OutPoint = new OutPoint(set.TransactionId, infoTransaction.Transaction.Index);
                            stakeTx.Address = infoTransaction.Address;
                            stakeTx.OutputIndex = infoTransaction.Transaction.Index;
                            stakeTx.HashBlock = this.chain.GetBlock((int)set.Height).HashBlock;
                            stakeTx.UtxoSet = set;
                            stakeTx.Secret = walletSecret; //temporary
                            stakeTxes.Add(stakeTx);
                        }
                    }
                }

                // Trying to sign a block
                if (this.SignBlock(stakeTxes, pblock, pindexPrev, pblocktemplate.TotalFee))
                {
                    var blockResult = new BlockResult {Block = pblock};
                    this.CheckState(new ContextInformation(blockResult, this.network.Consensus), pindexPrev);

                    pblocktemplate = null;
                }
                else
                {
                    Task.Delay(TimeSpan.FromMilliseconds(this.minerSleep), this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }
            }
        }

        private void CheckState(ContextInformation context, ChainedBlock pindexPrev)
        {
            var block = context.BlockResult.Block;

            if (!BlockStake.IsProofOfStake(block))
                return;

            // verify hash target and signature of coinstake tx
            var prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
            if (prevBlockStake == null)
                ConsensusErrors.PrevStakeNull.Throw();

            context.SetStake();
            this.posConsensusValidator.StakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], block.Header.Bits.ToCompact());

            // Found a solution
            if (block.Header.HashPrevBlock != this.chain.Tip.HashBlock)
                return;
            
            // validate the block
            this.consensusLoop.AcceptBlock(context);

            if (context.BlockResult.ChainedBlock == null) return; //reorg
            if (context.BlockResult.Error != null) return;

            if (context.BlockResult.ChainedBlock.ChainWork <= this.chain.Tip.ChainWork)
                return;

                // similar logic to what's in the full node code
            this.chain.SetTip(context.BlockResult.ChainedBlock);
            this.consensusLoop.Puller.SetLocation(this.consensusLoop.Tip);
            this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
            this.blockRepository.PutAsync(context.BlockResult.ChainedBlock.HashBlock, new List<Block> { block }).GetAwaiter().GetResult();
            this.signals.SignalBlock(block);

            this.logger.LogInformation($"==================================================================");
            this.logger.LogInformation($"Found new POS block hash={context.BlockResult.ChainedBlock.HashBlock} height={context.BlockResult.ChainedBlock.Height}");
            this.logger.LogInformation($"==================================================================");

            // wait for peers to get the block
            Thread.Sleep(1000);

            // ask peers for thier headers
            foreach (var node in this.connection.ConnectedNodes)
                node.Behavior<ChainHeadersBehavior>().TrySync();

            // wait for all peers to accept the block
            var retry = 0;
            foreach (var node in this.connection.ConnectedNodes)
            {
                var chainBehaviour = node.Behavior<ChainHeadersBehavior>();
                while (++retry < 100 && chainBehaviour.PendingTip != this.chain.Tip)
                    Thread.Sleep(1000);
            }

            if (retry == 100)
            {
                // seems the block was not accepted
                throw new MinerException("Block rejected by peers");
            }
        }

        // To decrease granularity of timestamp
        // Supposed to be 2^n-1

        private bool SignBlock(List<StakeTx> stakeTxes, Block block, ChainedBlock pindexBest, long fees)
        {
            // if we are trying to sign
            //    something except proof-of-stake block template
            if (!block.Transactions[0].Outputs[0].IsEmpty)
                return false;

            // if we are trying to sign
            //    a complete proof-of-stake block
            if (BlockStake.IsProofOfStake(block))
                return true;

            Key key = null;
            Transaction txCoinStake = new Transaction();

            txCoinStake.Time &= ~PosConsensusValidator.STAKE_TIMESTAMP_MASK;

            long searchTime = txCoinStake.Time; // search to current time


            if (searchTime > this.lastCoinStakeSearchTime)
            {
                long searchInterval = searchTime - this.lastCoinStakeSearchTime;
                if (this.CreateCoinStake(stakeTxes, pindexBest, block, searchInterval, fees, ref txCoinStake, ref key))
                {
                    if (txCoinStake.Time >= BlockValidator.GetPastTimeLimit(pindexBest) + 1)
                    {
                        // make sure coinstake would meet timestamp protocol
                        //    as it would be the same as the block timestamp
                        block.Transactions[0].Time = block.Header.Time = txCoinStake.Time;

                        // we have to make sure that we have no future timestamps in
                        //    our transactions set
                        foreach (var transaction in block.Transactions)
                            if (transaction.Time > block.Header.Time)
                                block.Transactions.Remove(transaction);

                        block.Transactions.Insert(1, txCoinStake);
                        block.UpdateMerkleRoot();

                        // append a signature to our block
                        var signature = key.Sign(block.GetHash());

                        block.BlockSignatur = new BlockSignature {Signature = signature.ToDER()};
                        return true;
                    }
                }

                this.LastCoinStakeSearchInterval = searchTime - this.LastCoinStakeSearchTime;
                this.LastCoinStakeSearchTime = searchTime;
            }

            return false;
        }


        public bool CreateCoinStake(List<StakeTx> stakeTxes, ChainedBlock pindexBest, Block block, long nSearchInterval,
            long fees, ref Transaction txNew, ref Key key)
        {
            var pindexPrev = pindexBest;
            var bnTargetPerCoinDay = new Target(block.Header.Bits).ToCompact();

            txNew.Inputs.Clear();
            txNew.Outputs.Clear();

            // Mark coin stake transaction
            txNew.Outputs.Add(new TxOut(Money.Zero, new Script()));

            // Choose coins to use
            var nBalance = this.GetBalance(stakeTxes).Satoshi;

            if (nBalance <= this.reserveBalance)
                return false;

            List<StakeTx> vwtxPrev = new List<StakeTx>();

            List<StakeTx> setCoins;
            long nValueIn = 0;

            // Select coins with suitable depth
            if (!this.SelectCoinsForStaking(stakeTxes, nBalance - this.reserveBalance, txNew.Time, out setCoins, out nValueIn))
                return false;

            if (!setCoins.Any())
                return false;

            this.logger.LogInformation($"Node staking with amount {new Money(setCoins.Sum(s => s.TxOut.Value))}"); //replace this with staking weight

            long nCredit = 0;
            Script scriptPubKeyKernel = null;
            
            // Note: I would expect to see coins sorted by weight on the original implementation 
            // sort the coins from heighest weight
            setCoins = setCoins.OrderByDescending(o => o.TxOut.Value).ToList();

            foreach (var coin in setCoins)
            {
                int maxStakeSearchInterval = 60;
                bool fKernelFound = false;

                for (uint n = 0; n < Math.Min(nSearchInterval, maxStakeSearchInterval) && !fKernelFound; n++)
                {
                    if (pindexPrev != this.chain.Tip)
                        return false;

                    try
                    {
                        var prevoutStake = new OutPoint(coin.UtxoSet.TransactionId, coin.OutputIndex);
                        long nBlockTime = 0;

                        var context = new ContextInformation(new BlockResult {Block = block}, this.network.Consensus);
                        context.SetStake();
                        this.posConsensusValidator.StakeValidator.CheckKernel(context, pindexPrev, block.Header.Bits, txNew.Time - n, prevoutStake, ref nBlockTime);

                        var timemaskceck = txNew.Time - n;

                        if ((timemaskceck & PosConsensusValidator.STAKE_TIMESTAMP_MASK) != 0)
                            continue;

                        if (context.Stake.HashProofOfStake != null)
                        {
                            scriptPubKeyKernel = coin.TxOut.ScriptPubKey;

                            key = null;
                            // calculate the key type
                            if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                            {
                                var outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
                                key = this.wallet.GetKeyForAddress(coin.Secret.WalletName, coin.Secret.WalletPassword, coin.Address).PrivateKey;
                            }
                            else if (PayToPubkeyHashTemplate.Instance.CheckScriptPubKey(scriptPubKeyKernel))
                            {
                                var outPubKey = scriptPubKeyKernel.GetDestinationAddress(this.network);
                                key = this.wallet.GetKeyForAddress(coin.Secret.WalletName, coin.Secret.WalletPassword, coin.Address).PrivateKey;
                            }
                            else
                            {
                                //LogPrint("coinstake", "CreateCoinStake : no support for kernel type=%d\n", whichType);
                                break; // only support pay to public key and pay to address
                            }

                            // create a pubkey script form the current script
                            var scriptPubKeyOut = PayToPubkeyTemplate.Instance.GenerateScriptPubKey(key.PubKey); //scriptPubKeyKernel;

                            coin.Key = key;
                            txNew.Time -= n;
                            txNew.AddInput(new TxIn(prevoutStake));
                            nCredit += coin.TxOut.Value;
                            vwtxPrev.Add(coin);
                            txNew.Outputs.Add(new TxOut(0, scriptPubKeyOut));

                            //LogPrint("coinstake", "CreateCoinStake : added kernel type=%d\n", whichType);
                            fKernelFound = true;
                            break;
                        }

                    }
                    catch (ConsensusErrorException cex)
                    {
                        if (cex.ConsensusError == ConsensusErrors.StakeHashInvalidTarget)
                            continue;
                        
                        throw;
                    }
                }

                if (fKernelFound)
                    break; // if kernel is found stop searching
            }

            if (nCredit == 0 || nCredit > nBalance - this.reserveBalance)
                return false;

            foreach (var coin in setCoins)
            {
                var cointrx = coin;
                //var coinIndex = coin.Value;

                // Attempt to add more inputs
                // Only add coins of the same key/address as kernel
                if (txNew.Outputs.Count == 2
                    && (
                        cointrx.TxOut.ScriptPubKey == scriptPubKeyKernel ||
                        cointrx.TxOut.ScriptPubKey == txNew.Outputs[1].ScriptPubKey
                    )
                    && cointrx.UtxoSet.TransactionId != txNew.Inputs[0].PrevOut.Hash)
                {
                    long nTimeWeight = BlockValidator.GetWeight((long)cointrx.UtxoSet.Time, (long)txNew.Time);

                    // Stop adding more inputs if already too many inputs
                    if (txNew.Inputs.Count >= 100)
                        break;
                    // Stop adding inputs if reached reserve limit
                    if (nCredit + cointrx.TxOut.Value > nBalance - this.reserveBalance)
                        break;
                    // Do not add additional significant input
                    if (cointrx.TxOut.Value >= GetStakeCombineThreshold())
                        continue;
                    // Do not add input that is still too young
                    if (BlockValidator.IsProtocolV3((int)txNew.Time))
                    {
                        // properly handled by selection function
                    }
                    else
                    {
                        if (nTimeWeight < BlockValidator.StakeMinAge)
                            continue;
                    }

                    txNew.Inputs.Add(new TxIn(new OutPoint(cointrx.UtxoSet.TransactionId, cointrx.OutputIndex)));

                    nCredit += cointrx.TxOut.Value;
                    vwtxPrev.Add(coin);
                }
            }

            // Calculate coin age reward
            ulong nCoinAge;
            if (!this.posConsensusValidator.StakeValidator.GetCoinAge(this.chain, this.coinView, txNew, pindexPrev, out nCoinAge))
                return false; //error("CreateCoinStake : failed to calculate coin age");

            long nReward = fees + this.posConsensusValidator.GetProofOfStakeReward(pindexPrev.Height);
            if (nReward <= 0)
                return false;

            nCredit += nReward;

            if (nCredit >= GetStakeSplitThreshold())
                txNew.Outputs.Add(new TxOut(0, txNew.Outputs[1].ScriptPubKey)); //split stake

            // Set output amount
            if (txNew.Outputs.Count == 3)
            {
                txNew.Outputs[1].Value = (nCredit / 2 / BlockValidator.CENT) * BlockValidator.CENT;
                txNew.Outputs[2].Value = nCredit - txNew.Outputs[1].Value;
            }
            else
                txNew.Outputs[1].Value = nCredit;

            // Sign
            foreach (var walletTx in vwtxPrev)
            {
                if (!this.SignSignature(walletTx, txNew))
                    return false; // error("CreateCoinStake : failed to sign coinstake");
            }

            // Limit size
            int nBytes = txNew.GetSerializedSize(ProtocolVersion.ALT_PROTOCOL_VERSION, SerializationType.Network);
            if (nBytes >= MAX_BLOCK_SIZE_GEN / 5)
                return false; // error("CreateCoinStake : exceeded coinstake size limit");

            // Successfully generated coinstake
            return true;
        }

        /** The maximum allowed size for a serialized block, in bytes (network rule) */
        public const int MAX_BLOCK_SIZE = 1000000;
        /** The maximum size for mined blocks */
        public const int MAX_BLOCK_SIZE_GEN = MAX_BLOCK_SIZE / 2;

        private bool SignSignature(StakeTx from, Transaction txTo, params Script[] knownRedeems)
        {
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
                return false;
            }

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
            var money = new Money(0);
            foreach (var stakeTx in stakeTxes)
            {
                // Must wait until coinbase is safely deep enough in the chain before valuing it
                if ((stakeTx.UtxoSet.IsCoinbase || stakeTx.UtxoSet.IsCoinstake) && this.GetBlocksToMaturity(stakeTx) > 0)
                    continue;

                money += stakeTx.TxOut.Value;
            }

            return money;
        }

        private bool SelectCoinsForStaking(List<StakeTx> stakeTxes,  long nTargetValue, uint nSpendTime, out List<StakeTx> setCoinsRet, out long nValueRet)
        {
            var coins = this.AvailableCoinsForStaking(stakeTxes, nSpendTime);
            setCoinsRet = new List<StakeTx>();
            nValueRet = 0;

            foreach (var output in coins)
            {
                var pcoin = output.StakeTx;
                //int i = output.Index;

                // Stop if we've chosen enough inputs
                if (nValueRet >= nTargetValue)
                    break;

                var n = pcoin.TxOut.Value;

                if (n >= nTargetValue)
                {
                    // If input value is greater or equal to target then simply insert
                    //    it into the current subset and exit
                    setCoinsRet.Add(pcoin);
                    nValueRet += n;
                    break;
                }
                else if (n < nTargetValue + BlockValidator.CENT)
                {
                    setCoinsRet.Add(pcoin);
                    nValueRet += n;
                }
            }

            return true;
        }

        private List<StakeOutput> AvailableCoinsForStaking(List<StakeTx> stakeTxes, uint nSpendTime)
        {
            var vCoins = new List<StakeOutput>();

            foreach (var pcoin in stakeTxes)
            {
                int nDepth = this.GetDepthInMainChain(pcoin);
                if (nDepth < 1)
                    continue;

                if (BlockValidator.IsProtocolV3((int)nSpendTime))
                {
                    if (nDepth < this.network.Consensus.Option<PosConsensusOptions>().StakeMinConfirmations)
                        continue;
                }
                else
                {
                    // Filtering by tx timestamp instead of block timestamp may give false positives but never false negatives
                    if (pcoin.UtxoSet.Time + this.network.Consensus.Option<PosConsensusOptions>().StakeMinAge > nSpendTime)
                        continue;
                }

                if (this.GetBlocksToMaturity(pcoin) > 0)
                    continue;

                if (pcoin.TxOut.Value >= this.minimumInputValue)
                {
                    // check if the coin is already staking
                    vCoins.Add(new StakeOutput { Depth = nDepth, StakeTx = pcoin });
                }
            }

            return vCoins;
        }

        private int GetBlocksToMaturity(StakeTx stakeTx)
        {
            if (!(stakeTx.UtxoSet.IsCoinbase || stakeTx.UtxoSet.IsCoinstake))
                return 0;

            return Math.Max(0, (int)this.network.Consensus.Option<PosConsensusOptions>().COINBASE_MATURITY + 1 - this.GetDepthInMainChain(stakeTx));
        }

        // Return depth of transaction in blockchain:
        // -1  : not in blockchain, and not in memory pool (conflicted transaction)
        //  0  : in memory pool, waiting to be included in a block
        // >=1 : this many blocks deep in the main chain
        private int GetDepthInMainChain(StakeTx stakeTx)
        {
            var chainedBlock = this.chain.GetBlock(stakeTx.HashBlock);

            if (chainedBlock == null)
                return -1;

            // TODO: check if in memory pool then return 0

            return this.chain.Tip.Height - chainedBlock.Height + 1;
        }
    }
}
