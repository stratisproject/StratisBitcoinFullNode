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
        public abstract void OnStart();

        public virtual void Kill()
        {
            this.FullNode?.Dispose();
            this.FullNode = null;
        }

        public void Start()
        {
            BuildNode();
            OnStart();
        }
    }
}