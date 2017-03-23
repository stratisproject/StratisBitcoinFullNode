﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using System;
using System.Threading;
using static Stratis.Bitcoin.FullNode;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusFeature : FullNodeFeature
    {
        private readonly DBreezeCoinView dBreezeCoinView;
        private readonly Network network;
        private readonly ConcurrentChain chain;
        private readonly ConsensusValidator consensusValidator;
        private readonly LookaheadBlockPuller blockPuller;
        private readonly CoinView coinView;
        private readonly ChainBehavior.ChainState chainState;
        private readonly ConnectionManager connectionManager;
        private readonly CancellationProvider globalCancellation;
        private readonly Signals signals;
        private readonly ConsensusLoop consensusLoop;
        
        public ConsensusFeature(
            DBreezeCoinView dBreezeCoinView, 
            Network network, 
            ConsensusValidator consensusValidator, 
            ConcurrentChain chain, 
            LookaheadBlockPuller blockPuller, 
            CoinView coinView, 
            ChainBehavior.ChainState chainState,
            ConnectionManager connectionManager,
            CancellationProvider globalCancellation,
            Signals signals,
            ConsensusLoop consensusLoop)
        {
            this.dBreezeCoinView = dBreezeCoinView;
            this.consensusValidator = consensusValidator;
            this.chain = chain;
            this.blockPuller = blockPuller;
            this.coinView = coinView;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.globalCancellation = globalCancellation;
            this.signals = signals;
            this.network = network;
            this.consensusLoop = consensusLoop;
        }

        public override void Start()
        {
            this.dBreezeCoinView.Initialize(network.GetGenesis()).GetAwaiter().GetResult();
            this.consensusLoop.Initialize();

            this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
            this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockPuller.BlockPullerBehavior(blockPuller));
            
            var flags = this.consensusLoop.GetFlags();
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);

            new Thread(RunLoop)
            {
                Name = "Consensus Loop"
            }.Start();
        }

        public override void Stop()
        {
            var cache = this.coinView as CachedCoinView;
            if (cache != null)
            {
                Logs.Consensus.LogInformation("Flushing Cache CoinView...");
                cache.FlushAsync().GetAwaiter().GetResult();
            }

            this.dBreezeCoinView.Dispose();            
        }

        private void RunLoop()
        {
            try
            {
                var stack = new CoinViewStack(this.coinView);
                var cache = stack.Find<CachedCoinView>();
                var stats = new ConsensusStats(stack, this.coinView, this.consensusLoop, this.chainState, this.chain, this.connectionManager);
                var cancellationToken = this.globalCancellation.Cancellation.Token;

                ChainedBlock lastTip = this.consensusLoop.Tip;
                foreach (var block in this.consensusLoop.Execute(cancellationToken))
                {
                    bool reorg = false;
                    if (this.consensusLoop.Tip.FindFork(lastTip) != lastTip)
                    {
                        reorg = true;
                        Logs.FullNode.LogInformation("Reorg detected, rewinding from " + lastTip.Height + " (" + lastTip.HashBlock + ") to " + this.consensusLoop.Tip.Height + " (" + this.consensusLoop.Tip.HashBlock + ")");
                    }
                    lastTip = this.consensusLoop.Tip;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (block.Error != null)
                    {
                        Logs.FullNode.LogError("Block rejected: " + block.Error.Message);

                        //Pull again
                        this.consensusLoop.Puller.SetLocation(this.consensusLoop.Tip);

                        if (block.Error == ConsensusErrors.BadWitnessNonceSize)
                        {
                            Logs.FullNode.LogInformation("You probably need witness information, activating witness requirement for peers.");
                            this.connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);
                            this.consensusLoop.Puller.RequestOptions(TransactionOptions.Witness);
                            continue;
                        }

                        //Set the PoW chain back to ConsensusLoop.Tip
                        this.chain.SetTip(this.consensusLoop.Tip);
                        //Since ChainBehavior check PoW, MarkBlockInvalid can't be spammed
                        Logs.FullNode.LogError("Marking block as invalid");
                        this.chainState.MarkBlockInvalid(block.ChainedBlock.HashBlock);
                    }

                    if (!reorg && block.Error == null)
                    {
                        this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
                        if (this.chain.Tip.HashBlock == block.ChainedBlock?.HashBlock)
                        {
                            var unused = cache.FlushAsync();
                        }

                        this.signals.Blocks.Broadcast(block.Block);
                    }

                    // TODO: replace this with a signalling object
                    if (stats.CanLog)
                        stats.Log();
                }
            }
            catch (Exception ex) 
            {
                if (ex is OperationCanceledException)
                {
                    if (this.globalCancellation.Cancellation.IsCancellationRequested)
                        return;
                }

                // TODO Need to revisit unhandled exceptions in a way that any process can signal an exception has been
                // thrown so that the node and all the disposables can stop gracefully.
                Logs.Consensus.LogCritical(new EventId(0), ex, "Consensus loop unhandled exception (Tip:" + this.consensusLoop.Tip?.Height + ")");
                throw;
            }
        }        
    }

    public static class ConsensusFeatureExtension
    {
        public static IFullNodeBuilder UseConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ConsensusFeature>()
                .FeatureServices(services =>
                {
                    var dataFolder = new DataFolder(fullNodeBuilder.NodeSettings);
                    var coinviewdb = new DBreezeCoinView(fullNodeBuilder.Network, dataFolder.CoinViewPath);
               
                    services.AddSingleton(new ConsensusValidator(fullNodeBuilder.Network.Consensus));
                    services.AddSingleton(coinviewdb);
                    services.AddSingleton<CoinView>(new CachedCoinView(coinviewdb) { MaxItems = fullNodeBuilder.NodeSettings.Cache.MaxItems });
                    services.AddSingleton<LookaheadBlockPuller>();
                    services.AddSingleton<ConsensusLoop>();
                });
            });

            return fullNodeBuilder;
        }
    }    
}
