using System.IO;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class SmartContractCoreNode : CoreNode
    {
        public SmartContractCoreNode(string folder, INodeRunner runner, NodeBuilder builder, Network network, bool cleanfolders = true, string configfile = "bitcoin.conf") : base(folder, runner, builder, network, cleanfolders, configfile)
        {
        }

        public override async Task StartAsync()
        {
            NodeConfigParameters config = new NodeConfigParameters();
            config.Add("scregtest", "1");
            config.Add("rest", "1");
            config.Add("server", "1");
            config.Add("txindex", "1");
            config.Add("rpcuser", this.creds.UserName);
            config.Add("rpcpassword", this.creds.Password);
            config.Add("port", this.ports[0].ToString());
            config.Add("rpcport", this.ports[1].ToString());
            config.Add("printtoconsole", "1");
            config.Add("keypool", "10");
            config.Import(this.ConfigParameters);
            File.WriteAllText(this.Config, config.ToString());
            lock (this.lockObject)
            {
                this.runner.Start(this.DataFolder);
                this.State = CoreNodeState.Starting;
            }
            while (true)
            {
                try
                {
                    await this.CreateRPCClient().GetBlockHashAsync(0);
                    this.State = CoreNodeState.Running;
                    break;
                }
                catch
                {
                }
                if (this.runner.IsDisposed)
                    break;
            }
        }
    }
}
