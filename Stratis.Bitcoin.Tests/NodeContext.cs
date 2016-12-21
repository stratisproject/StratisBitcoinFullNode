using NBitcoin;
using Stratis.Bitcoin.Consensus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests
{
	public class NodeContext : IDisposable
	{
		private string name;
		public string FolderName
		{
			get
			{
				return name;
			}
		}

		List<IDisposable> _CleanList = new List<IDisposable>();

		public NodeContext(string name, Network network, bool clean)
		{
			Clean = clean;
			network = network ?? Network.RegTest;
			this.name = name;
			if(Clean)
				CleanDirectory();
			_Network = network;

			_PersistentCoinView = new DBreezeCoinView(network, name);
			_CleanList.Add(_PersistentCoinView);
		}


		private readonly Network _Network;
		public Network Network
		{
			get
			{
				return _Network;
			}
		}


		private ChainBuilder _ChainBuilder;
		public ChainBuilder ChainBuilder
		{
			get
			{
				return _ChainBuilder = _ChainBuilder ?? new ChainBuilder(Network);
			}
		}
		
		DBreezeCoinView _PersistentCoinView;
		public DBreezeCoinView PersistentCoinView
		{
			get
			{
				return _PersistentCoinView;
			}
		}

		public bool Clean
		{
			get;
			private set;
		}

		private void CleanDirectory()
		{
			try
			{
				Directory.Delete(name, true);
			}
			catch(DirectoryNotFoundException)
			{
			}
		}

		public static NodeContext Create([CallerMemberNameAttribute]string name = null, Network network = null, bool clean = true)
		{
			return new NodeContext(name, network, clean);
		}

		public void Dispose()
		{
			foreach(var item in _CleanList)
				item.Dispose();
			if(Clean)
				CleanDirectory();
		}

		public void ReloadPersistentCoinView()
		{
			_PersistentCoinView.Dispose();
			_CleanList.Remove(_PersistentCoinView);
			_PersistentCoinView = new DBreezeCoinView(_Network, name);
			_CleanList.Add(_PersistentCoinView);
		}		
	}
}
