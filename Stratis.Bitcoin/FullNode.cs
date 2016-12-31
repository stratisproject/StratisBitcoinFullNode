using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Stratis.Bitcoin.RPC;
using NBitcoin;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Consensus;
using NBitcoin.Protocol;
using Microsoft.AspNetCore.Hosting.Internal;

namespace Stratis.Bitcoin
{
	public class FullNode : IDisposable
	{
		NodeArgs _Args;
		public FullNode(NodeArgs args)
		{
			if(args == null)
				throw new ArgumentNullException("args");
			_Args = args;
			Network = _Args.GetNetwork();
		}

		CancellationTokenSource _Cancellation;

		public Network Network
		{
			get;
			internal set;
		}

		public CoinView CoinView
		{
			get; set;
		}

		public DataFolder DataFolder
		{
			get; set;
		}
		public void Start()
		{
			DataFolder = new DataFolder(_Args.DataDir);
			CoinView = new CachedCoinView(new DBreezeCoinView(Network, DataFolder.CoinViewPath));
			_Cancellation = new CancellationTokenSource();
			if(_Args.RPC != null)
			{
				RPCHost = new WebHostBuilder()
				.UseKestrel()
				.ForFullNode(this)
				.UseUrls(_Args.RPC.GetUrls())
				.UseIISIntegration()
				.UseStartup<RPC.Startup>()
				.Build();
				RPCHost.Start();
				Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + String.Join(Environment.NewLine, _Args.RPC.GetUrls()));
			}

			StartFlushAddrManThread();
			StartFlushChainThread();
		}

		public IWebHost RPCHost
		{
			get; set;
		}

		private void StartFlushChainThread()
		{
			if(!Directory.Exists(DataFolder.ChainPath))
			{
				Logs.FullNode.LogInformation("Creating " + DataFolder.ChainPath);
				Directory.CreateDirectory(DataFolder.ChainPath);				
			}
			ChainRepository = new ChainRepository(DataFolder.ChainPath);
			Logs.FullNode.LogInformation("Loading chain");
			Chain = ChainRepository.GetChain().GetAwaiter().GetResult();
			Chain = Chain ?? new ConcurrentChain(Network);
			Logs.FullNode.LogInformation("Chain loaded at height " + Chain.Height);
			FlushChainTask = new PeriodicTask("FlushChain", (cancellation) =>
			{
				ChainRepository.Save(Chain);
			}).Start(_Cancellation.Token);
		}

		public AddressManager AddressManager
		{
			get; set;
		}

		public ChainRepository ChainRepository
		{
			get; set;
		}

		/// <summary>
		/// The longest PoW chain
		/// </summary>
		public ConcurrentChain Chain
		{
			get; set;
		}

		public PeriodicTask FlushAddrmanTask
		{
			get; set;
		}

		public PeriodicTask FlushChainTask
		{
			get; set;
		}

		bool _IsDisposed;
		public bool IsDisposed
		{
			get
			{
				return _IsDisposed;
			}
		}

		private void StartFlushAddrManThread()
		{
			if(!File.Exists(DataFolder.AddrManFile))
			{
				Logs.FullNode.LogInformation("Creating " + DataFolder.AddrManFile);
				AddressManager = new AddressManager();
				AddressManager.SavePeerFile(DataFolder.AddrManFile, Network);
			}
			else
			{
				Logs.FullNode.LogInformation("Loading addrman");
				AddressManager = AddressManager.LoadPeerFile(DataFolder.AddrManFile);
				Logs.FullNode.LogInformation("Loaded");
			}
			FlushAddrmanTask = new PeriodicTask("FlushAddrMan", (cancellation) =>
			{
				AddressManager.SavePeerFile(DataFolder.AddrManFile, Network);
			}).Start(_Cancellation.Token);
		}

		public void Dispose()
		{
			RPCHost.Dispose();
			_Cancellation.Cancel();
			FlushAddrmanTask.RunAndStop();
			Logs.FullNode.LogInformation("FlushAddrMan stopped");
			FlushChainTask.RunAndStop();
			Logs.FullNode.LogInformation("FlushChain stopped");
			_IsDisposed = true;
		}
	}
}
