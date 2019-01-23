using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    /// <summary>
    /// A feature that can be added to test nodes to execute various actions when a certain block has been connected or disconnected.
    /// </summary>
    public sealed class BlockObserverFeature : FullNodeFeature
    {
        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMiningExtension
    {
        /// <summary>
        /// Adds a feature to the node that will observe when blocks are connected or disconnected.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddBlockObserverFeature(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<BlockObserverFeature>();
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds a feature to the node that will observe when blocks are connected or disconnected.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="interceptor">Callback routine to be called when a certain block has been disconnected.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder UseDisconnectedInterceptor(this IFullNodeBuilder fullNodeBuilder, Action<ChainedHeaderBlock> interceptor)
        {
            fullNodeBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(service => new InterceptBlockDisconnected(service.GetService<Signals.Signals>(), interceptor));
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds a feature to the node that will observe when blocks are connected or disconnected.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="interceptor">Callback routine to be called when a certain block has been disconnected.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder UseConnectedInterceptor(this IFullNodeBuilder fullNodeBuilder, Action<ChainedHeaderBlock> interceptor)
        {
            fullNodeBuilder.ConfigureServices(services =>
            {
                services.AddSingleton(service => new InterceptBlockConnected(service.GetService<Signals.Signals>(), interceptor));
            });

            return fullNodeBuilder;
        }
    }

    /// <summary>
    /// Executes an action when a block has been disconnected at a certain height. 
    /// </summary>
    public sealed class InterceptBlockDisconnected : SignalObserver<ChainedHeaderBlock>
    {
        private readonly Action<ChainedHeaderBlock> interceptor;

        public InterceptBlockDisconnected(Signals.Signals signals, Action<ChainedHeaderBlock> interceptor)
        {
            signals.SubscribeForBlocksDisconnected(this);
            this.interceptor = interceptor;
        }

        /// <summary>
        /// Execution of the interceptor will only happen once in this implementation.
        /// </summary>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.interceptor?.Invoke(chainedHeaderBlock);
        }
    }

    /// <summary>
    /// Executes an action when a block has been connected at a certain height. 
    /// </summary>
    public sealed class InterceptBlockConnected : SignalObserver<ChainedHeaderBlock>
    {
        private readonly Action<ChainedHeaderBlock> interceptor;

        public InterceptBlockConnected(Signals.Signals signals, Action<ChainedHeaderBlock> interceptor)
        {
            signals.SubscribeForBlocksConnected(this);
            this.interceptor = interceptor;
        }

        /// <summary>
        /// Execution of the interceptor will only happen once in this implementation.
        /// </summary>
        protected override void OnNextCore(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.interceptor?.Invoke(chainedHeaderBlock);
        }
    }
}
