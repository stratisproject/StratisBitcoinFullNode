using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Breeze.Api
{
	/// <summary>
	/// Provides an Api to the full node
	/// </summary>
	public class ApiFeature : FullNodeFeature
	{		
		private readonly IFullNodeBuilder fullNodeBuilder;
		private readonly FullNode fullNode;

		public ApiFeature(IFullNodeBuilder fullNodeBuilder, FullNode fullNode)
		{
			this.fullNodeBuilder = fullNodeBuilder;
			this.fullNode = fullNode;
		}

		public override void Start()
		{
			Program.Initialize(this.fullNodeBuilder.Services, this.fullNode);
		}
	}

	public static class ApiFeatureExtension
	{
		public static IFullNodeBuilder UseApi(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
				.AddFeature<ApiFeature>()
				.FeatureServices(services =>
					{
						services.AddSingleton(fullNodeBuilder);
					});
			});

			return fullNodeBuilder;
		}
	}	
}
