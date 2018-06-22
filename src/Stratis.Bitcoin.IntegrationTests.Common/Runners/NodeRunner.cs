namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public abstract class NodeRunner
    {
        public readonly string DataFolder;
        public bool IsDisposed => this.FullNode.State == FullNodeState.Disposed;
        public FullNode FullNode { get; set; }

        protected NodeRunner(string dataDir)
        {
            this.DataFolder = dataDir;
        }

        public abstract void BuildNode();
        public abstract void OnStart();

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start()
        {
            BuildNode();
            OnStart();
        }
    }
}