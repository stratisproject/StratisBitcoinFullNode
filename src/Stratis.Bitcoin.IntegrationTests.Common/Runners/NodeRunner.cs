using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public abstract class NodeRunner
    {
        public readonly string DataFolder;

        public readonly string Agent;

        public bool IsDisposed
        {
            get
            {
                return this.FullNode == null || this.FullNode.State == FullNodeState.Disposed;
            }
        }

        public bool EnablePeerDiscovery { get; internal set; }
        public FullNode FullNode { get; set; }

        public Action<ChainedHeaderBlock> InterceptorDisconnect { get; internal set; }
        public Func<ChainedHeaderBlock, bool> InterceptorDisconnectOnce { get; internal set; }

        public Action<ChainedHeaderBlock> InterceptorConnect { get; internal set; }
        public Func<ChainedHeaderBlock, bool> InterceptorConnectOnce { get; internal set; }

        public Network Network { set; get; }
        public bool OverrideDateTimeProvider { get; internal set; }
        public Action<IServiceCollection> ServiceToOverride { get; internal set; }

        protected NodeRunner(string dataDir, string agent)
        {
            this.DataFolder = dataDir;
            this.Agent = agent;
        }

        public abstract void BuildNode();

        public virtual void Start()
        {
            if (this.FullNode == null)
            {
                throw new Exception("You can only start a full node after you've called BuildNode().");
            }

            this.FullNode.Start();
        }

        public virtual void Stop()
        {
            if (!this.IsDisposed)
            {
                this.FullNode?.Dispose();
            }

            this.FullNode = null;
        }

        protected void ConfigureInterceptors(IFullNodeBuilder builder)
        {
            if (this is BitcoinCoreRunner)
                return;

            builder = builder.AddBlockObserverFeature();

            if (this.InterceptorConnect != null)
                builder = builder.UseBlockConnectedInterceptor(this.InterceptorConnect);

            if (this.InterceptorConnectOnce != null)
                builder = builder.UseBlockConnectedInterceptorOnce(this.InterceptorConnectOnce);

            if (this.InterceptorDisconnect != null)
                builder = builder.UseBlockDisconnectedInterceptor(this.InterceptorDisconnect);

            if (this.InterceptorDisconnectOnce != null)
                builder = builder.UseBlockDisconnectedInterceptorOnce(this.InterceptorDisconnectOnce);
        }
    }
}