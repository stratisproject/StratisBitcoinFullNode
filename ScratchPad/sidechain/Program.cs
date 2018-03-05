using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Api;

namespace Stratis.Sidechains
{
    class Program
    {
        static void Main(string[] args)
        {
	        Network network = Network.SidechainTest;
	        NodeSettings nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);

			var node = new FullNodeBuilder()
		        .UseNodeSettings(nodeSettings)
		        .Build();

	        node.Run();
		}
    }
}
