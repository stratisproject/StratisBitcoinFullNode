using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Broadcasting;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Controllers;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Notifications;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
{
	/// <summary>
	/// Wallet feature for the full node.
	/// </summary>
	/// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
	/// <seealso cref="Stratis.Bitcoin.Interfaces.INodeStats" />
	public class GeneralPurposeWalletFeature : FullNodeFeature, INodeStats, IFeatureStats
	{
		private readonly IGeneralPurposeWalletSyncManager walletSyncManager;

		private readonly IGeneralPurposeWalletManager walletManager;

		private readonly Signals.Signals signals;

		private IDisposable blockSubscriberDisposable;

		private IDisposable transactionSubscriberDisposable;

		private ConcurrentChain chain;

		private readonly IConnectionManager connectionManager;

		private readonly BroadcasterBehavior broadcasterBehavior;

		/// <summary>
		/// Initializes a new instance of the <see cref="WalletFeature"/> class.
		/// </summary>
		/// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
		/// <param name="walletManager">The wallet manager.</param>
		/// <param name="signals">The signals responsible for receiving blocks and transactions from the network.</param>
		/// <param name="chain">The chain of blocks.</param>
		/// <param name="connectionManager">The connection manager.</param>
		/// <param name="broadcasterBehavior">The broadcaster behavior.</param>
		public GeneralPurposeWalletFeature(
			IGeneralPurposeWalletSyncManager walletSyncManager,
			IGeneralPurposeWalletManager walletManager,
			Signals.Signals signals,
			ConcurrentChain chain,
			IConnectionManager connectionManager,
			BroadcasterBehavior broadcasterBehavior)
		{
			this.walletSyncManager = walletSyncManager;
			this.walletManager = walletManager;
			this.signals = signals;
			this.chain = chain;
			this.connectionManager = connectionManager;
			this.broadcasterBehavior = broadcasterBehavior;
		}

		/// <inheritdoc />
		public void AddNodeStats(StringBuilder benchLogs)
		{
			GeneralPurposeWalletManager walletManager = this.walletManager as GeneralPurposeWalletManager;

			if (walletManager != null)
			{
				int height = walletManager.LastBlockHeight();
				ChainedBlock block = this.chain.GetBlock(height);
				uint256 hashBlock = block == null ? 0 : block.HashBlock;

				benchLogs.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
										(walletManager.ContainsWallets ? height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
										(walletManager.ContainsWallets ? (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashBlock) : string.Empty));
			}
		}

        /// <inheritdoc />
        public void AddFeatureStats(StringBuilder benchLog)
        {
            var walletNames = this.walletManager.GetWalletsNames();

            if (walletNames.Any())
            {
                benchLog.AppendLine();
                benchLog.AppendLine("====== General Purpose Wallets======");

                foreach (var walletName in walletNames)
                {
                    var items = this.walletManager.GetSpendableTransactionsInWallet(walletName, 1);
                    benchLog.AppendLine("Wallet: " + (walletName + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + new Money(items.Sum(s => s.Transaction.Amount)).ToString());
                }

                benchLog.AppendLine();
                benchLog.AppendLine("====== Multisig Balances======");

                foreach (var walletName in walletNames)
                {
                    GeneralPurposeWallet wallet = this.walletManager.GetWalletByName(walletName);
                    foreach (var account in wallet.GetAccountsByCoinType((CoinType)this.chain.Network.Consensus.CoinType))
                    {
                        foreach (MultiSigAddress multiSigAddress in account.MultiSigAddresses)
                        {
                            List<TransactionData> allTransactions = multiSigAddress.UnspentTransactions().ToList();

                            var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
                            var total = allTransactions.Sum(t => t.SpendableAmount(false));

                            benchLog.AppendLine("Address: " + (multiSigAddress.Address + ",").PadRight(LoggingConfiguration.ColumnLength) + " Confirmed balance: " + confirmed);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void Initialize()
		{
			// subscribe to receiving blocks and transactions
			this.blockSubscriberDisposable = this.signals.SubscribeForBlocks(new BlockObserver(this.walletSyncManager));
			this.transactionSubscriberDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.walletSyncManager));

			this.walletManager.Start();
			this.walletSyncManager.Start();

			this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
		}

		/// <inheritdoc />
		public override void Dispose()
		{
			this.blockSubscriberDisposable.Dispose();
			this.transactionSubscriberDisposable.Dispose();

			this.walletManager.Stop();
			this.walletSyncManager.Stop();
		}
	}

	/// <summary>
	/// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
	/// </summary>
	public static class FullNodeBuilderWalletExtension
	{
		public static IFullNodeBuilder UseGeneralPurposeWallet(this IFullNodeBuilder fullNodeBuilder)
		{
			LoggingConfiguration.RegisterFeatureNamespace<GeneralPurposeWalletFeature>("generalpurposewallet");

			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
				.AddFeature<GeneralPurposeWalletFeature>()
				.DependOn<MempoolFeature>()
				.DependOn<BlockStoreFeature>()
				.DependOn<RPCFeature>()
				.FeatureServices(services =>
				{
					services.AddSingleton<IGeneralPurposeWalletSyncManager, GeneralPurposeWalletSyncManager>();
					services.AddSingleton<IGeneralPurposeWalletTransactionHandler, GeneralPurposeWalletTransactionHandler>();
					services.AddSingleton<IGeneralPurposeWalletManager, GeneralPurposeWalletManager>();
					services.AddSingleton<IGeneralPurposeWalletFeePolicy, GeneralPurposeWalletFeePolicy>();
					services.AddSingleton<GeneralPurposeWalletController>();
					//services.AddSingleton<WalletRPCController>();
					//services.AddSingleton<IBroadcasterManager, GeneralPurposeFullNodeBroadcasterManager>();
					//services.AddSingleton<BroadcasterBehavior>();
				});
			});

			return fullNodeBuilder;
		}
	}
}
