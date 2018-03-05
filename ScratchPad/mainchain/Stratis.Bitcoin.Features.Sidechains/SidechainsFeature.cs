using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Sidechains.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.Sidechains
{
	public class SidechainsFeature : FullNodeFeature
	{
		private Network network;
		private IWatchOnlyWalletManager watchOnlyWalletManager;

		public SidechainsFeature(ILoggerFactory loggerFactory, NodeSettings nodeSettings,
			IWalletManager walletManager, IWatchOnlyWalletManager watchOnlyWalletManager, ConcurrentChain chain,
			Network network, Signals.Signals signals, IWalletTransactionHandler walletTransactionHandler, 
			IWalletSyncManager walletSyncManager, IWalletFeePolicy walletFeePolicy, IBroadcasterManager broadcasterManager,
			FullNode fullNode)
		{
			this.network = network;
			this.watchOnlyWalletManager = watchOnlyWalletManager;
		}

		public override void Start()
		{
			string SIDECHAIN_ADDRESS = "TNYBX53K9e1SHSy4tBr3o99rYKSKyihvFg";
			this.watchOnlyWalletManager.Initialize();
			this.watchOnlyWalletManager.WatchAddress(SIDECHAIN_ADDRESS);
		}
	}

	public static partial class IFullNodeBuilderExtensions
	{
		public static IFullNodeBuilder UseSidechains(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<SidechainsFeature>()
					.FeatureServices(services =>
					{
						//services.AddSingleton<ISidechainActor, SidechainActor>();
						services.AddSingleton<SidechainsController>();
					});
			});

			return fullNodeBuilder;
		}
	}
}
