using System;
using System.Threading;
using NBitcoin;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public abstract class NodeRunner
    {
        public readonly string DataFolder;

        public bool IsDisposed
        {
            get
            {
                return this.FullNode == null || this.FullNode.State == FullNodeState.Disposed;
            }
        }

        public FullNode FullNode { get; set; }

        public Network Network { set; get; }

        protected NodeRunner(string dataDir)
        {
            this.DataFolder = dataDir;
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
    }
}