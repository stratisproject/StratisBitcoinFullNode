using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class BlockResult
    {
        public ChainedBlock ChainedBlock { get; set; }
        public Block Block { get; set; }
        public ConsensusError Error { get; set; }
    }
    
    public class ConsensusLoop
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public StakeChain StakeChain { get; }
        public LookaheadBlockPuller Puller { get; }
        public ConcurrentChain Chain { get; }
        public CoinView UTXOSet { get; }
        public PowConsensusValidator Validator { get; }
        public ChainedBlock Tip { get; private set; }
        public NodeDeployments NodeDeployments { get; private set; }

        public ConsensusLoop(PowConsensusValidator validator, ConcurrentChain chain, CoinView utxoSet, LookaheadBlockPuller puller, NodeDeployments nodeDeployments, ILoggerFactory loggerFactory, StakeChain stakeChain = null)
        {
            Guard.NotNull(validator, nameof(validator));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(utxoSet, nameof(utxoSet));
            Guard.NotNull(puller, nameof(puller));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.Validator = validator;
            this.Chain = chain;
            this.UTXOSet = utxoSet;
            this.Puller = puller;
            this.NodeDeployments = nodeDeployments;

            // chain of stake info can be null if POS is not enabled
            this.StakeChain = stakeChain;
        }

        public void Initialize()
        {
            this.logger.LogTrace("()");

            uint256 utxoHash = this.UTXOSet.GetBlockHashAsync().GetAwaiter().GetResult();
            while (true)
            {
                this.Tip = this.Chain.GetBlock(utxoHash);
                if (this.Tip != null)
                    break;

                utxoHash = this.UTXOSet.Rewind().GetAwaiter().GetResult();
            }
            this.Puller.SetLocation(this.Tip);

            this.logger.LogTrace("(-)");
        }

        public IEnumerable<BlockResult> Execute(CancellationToken cancellationToken)
        {
            while (true)
            {
                yield return this.ExecuteNextBlock(cancellationToken);
            }
        }

        public BlockResult ExecuteNextBlock(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("()");

            BlockResult result = new BlockResult();
            try
            {
                using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockFetchingTime(o)))
                {
                    while (true)
                    {
                        result.Block = this.Puller.NextBlock(cancellationToken);
                        if (result.Block != null)
                        {
                            this.logger.LogTrace("Block received from puller.");
                            break;
                        }

                        this.logger.LogTrace("No block received from puller due to reorganization, rewinding.");
                        while (true)
                        {
                            uint256 hash = this.UTXOSet.Rewind().GetAwaiter().GetResult();
                            ChainedBlock rewinded = this.Chain.GetBlock(hash);
                            if (rewinded == null)
                            {
                                this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.", hash);
                                continue;
                            }

                            this.logger.LogTrace("Rewound to '{0}'.", hash);
                            this.Tip = rewinded;
                            this.Puller.SetLocation(rewinded);
                            break;
                        }
                    }
                }

                this.AcceptBlock(new ContextInformation(result, this.Tip, this.Validator.ConsensusParams));
            }
            catch (ConsensusErrorException ex)
            {
                result.Error = ex.ConsensusError;
            }

            this.logger.LogTrace("(-):*.{0}='{1}/{2}',*.{3}='{4}'", nameof(result.ChainedBlock), result.ChainedBlock?.HashBlock, result.ChainedBlock?.Height, nameof(result.Error), result.Error?.Message);
            return result;
        }

        public void AcceptBlock(ContextInformation context)
        {
            this.logger.LogTrace("()");

            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                // Check that the current block has not been reorged.
                // Catching a reorg at this point will not require a rewind.
                if (context.BlockResult.Block.Header.HashPrevBlock != this.Tip.HashBlock)
                {
                    this.logger.LogTrace("Reorganization detected.");
                    ConsensusErrors.InvalidPrevTip.Throw(); // reorg
                }

                this.logger.LogTrace("Validating new block.");

                // Build the next block in the chain of headers. The chain header is most likely already created by 
                // one of the peers so after we create a new chained block (mainly for validation) 
                // we ask the chain headers for its version (also to prevent memory leaks). 
                context.BlockResult.ChainedBlock = new ChainedBlock(context.BlockResult.Block.Header, context.BlockResult.Block.Header.GetHash(), this.Tip);
                
                // Liberate from memory the block created above if possible.
                context.BlockResult.ChainedBlock = this.Chain.GetBlock(context.BlockResult.ChainedBlock.HashBlock) ?? context.BlockResult.ChainedBlock;
                context.SetBestBlock();

                // == validation flow ==
                
                // Check the block header is correct.
                this.Validator.CheckBlockHeader(context);
                this.Validator.ContextualCheckBlockHeader(context);

                // Calculate the consensus flags and check they are valid.
                context.Flags = this.NodeDeployments.GetFlags(context.BlockResult.ChainedBlock);
                this.Validator.ContextualCheckBlock(context);

                // check the block itself
                this.Validator.CheckBlock(context);
            }

            if (context.OnlyCheck)
            {
                this.logger.LogTrace("(-)[CHECK_ONLY]");
                return;
            }

            // Load the UTXO set of the current block. UTXO may be loaded from cache or from disk.
            // The UTXO set is stored in the context.
            this.logger.LogTrace("Loading UTXO set of the new block.");
            context.Set = new UnspentOutputSet();
            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddUTXOFetchingTime(o)))
            {
                uint256[] ids = GetIdsToFetch(context.BlockResult.Block, context.Flags.EnforceBIP30);
                FetchCoinsResponse coins = this.UTXOSet.FetchCoinsAsync(ids).GetAwaiter().GetResult();
                context.Set.SetCoins(coins.UnspentOutputs);
            }

            // Attempt to load into the cache the next set of UTXO to be validated.
            // The task is not awaited so will not stall main validation process.
            this.TryPrefetchAsync(context.Flags);

            // Validate the UTXO set is correctly spent.
            this.logger.LogTrace("Executing block.");
            using (new StopwatchDisposable(o => this.Validator.PerformanceCounter.AddBlockProcessingTime(o)))
            {
                this.Validator.ExecuteBlock(context, null);
            }

            // Persist the changes to the coinview. This will likely only be stored in memory, 
            // unless the coinview treashold is reached.
            this.logger.LogTrace("Saving coinview changes.");
            this.UTXOSet.SaveChangesAsync(context.Set.GetCoins(this.UTXOSet), null, this.Tip.HashBlock, context.BlockResult.ChainedBlock.HashBlock).GetAwaiter().GetResult();

            // Set the new tip.
            this.Tip = context.BlockResult.ChainedBlock;
            this.logger.LogTrace("(-)[OK]");
        }

        public Task FlushAsync()
        {
            this.logger.LogTrace("()");

            Task res = (this.UTXOSet as CachedCoinView)?.FlushAsync();

            this.logger.LogTrace("(-)");
            return res;
        }

        private Task TryPrefetchAsync(DeploymentFlags flags)
        {
            this.logger.LogTrace("({0}:{1})", nameof(flags), flags);

            Task prefetching = Task.FromResult<bool>(true);

            if (this.UTXOSet is CachedCoinView)
            {
                Block nextBlock = this.Puller.TryGetLookahead(0);
                if (nextBlock != null)
                    prefetching = this.UTXOSet.FetchCoinsAsync(GetIdsToFetch(nextBlock, flags.EnforceBIP30));
            }

            this.logger.LogTrace("(-)");
            return prefetching;
        }

        public uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(block), block.GetHash(), nameof(enforceBIP30), enforceBIP30);

            HashSet<uint256> ids = new HashSet<uint256>();
            foreach (Transaction tx in block.Transactions)
            {
                if (enforceBIP30)
                {
                    var txId = tx.GetHash();
                    ids.Add(txId);
                }

                if (!tx.IsCoinBase)
                {
                    foreach (TxIn input in tx.Inputs)
                    {
                        ids.Add(input.PrevOut.Hash);
                    }
                }
            }

            uint256[] res = ids.ToArray();
            this.logger.LogTrace("(-):*.{0}={1}", nameof(res.Length), res.Length);
            return res;
        }
    }
}
