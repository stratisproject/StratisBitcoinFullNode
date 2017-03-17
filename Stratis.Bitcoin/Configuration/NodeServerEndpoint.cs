using System.Net;

namespace Stratis.Bitcoin.Configuration
{
	public class NodeServerEndpoint
	{
		public NodeServerEndpoint()
		{

		}
		public NodeServerEndpoint(IPEndPoint endpoint, bool whitelisted)
		{
			Endpoint = endpoint;
			Whitelisted = whitelisted;
		}
		public IPEndPoint Endpoint
		{
			get; set;
		}
		public bool Whitelisted
		{
			get; set;
		}
	}
}
