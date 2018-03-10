using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using BreezeCommon;

namespace Stratis.MasterNode.Features.InterNodeComms
{
	public class InterNodeCommunicationFeature : FullNodeFeature
	{
		private readonly IConnectionManager connectionManager;
		private readonly INodeLifetime nodeLifetime;
        private NodeSettings nodeSettings;
		private RegistrationStore registrationStore;

        public InterNodeCommunicationFeature(IConnectionManager connectionManager, INodeLifetime nodeLifetime, NodeSettings nodeSettings, RegistrationStore registrationStore)
		{
			this.connectionManager = connectionManager;
			this.nodeLifetime = nodeLifetime;
            this.nodeSettings = nodeSettings;
			this.registrationStore = registrationStore;

            // Force registration store to be kept in same folder as other node data
            this.registrationStore.SetStorePath(this.nodeSettings.DataDir);
		}

		public override void Start()
		{
			NodeConnectionParameters connectionParameters = this.connectionManager.Parameters;
			connectionParameters.TemplateBehaviors.Add(new ServiceDiscoveryBehavior(new List<RegistrationCapsule>(this.registrationStore.GetAllAsCapsules()), this.registrationStore));
		}
	}
    
	public static class InterNodeCommsFeatureExtension
	{
		public static IFullNodeBuilder UseInterNodeCommunication(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<InterNodeCommunicationFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<RegistrationStore>();
                        //services.AddSingleton<Network>();
                });
			});
			return fullNodeBuilder;
		}
	}
}