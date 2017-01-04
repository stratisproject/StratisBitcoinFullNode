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
		List<IDisposable> _CleanList = new List<IDisposable>();
		TestDirectory _TestDirectory;
		public NodeContext(string name, Network network, bool clean)
		{
			network = network ?? Network.RegTest;
			_Network = network;
			_TestDirectory = new TestDirectory(name, clean);
			_PersistentCoinView = new DBreezeCoinView(network, _TestDirectory.FolderName);
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

		public string FolderName
		{
			get
			{
				return _TestDirectory.FolderName;
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
			_TestDirectory.Dispose(); //Not into cleanlist because it must run last
		}

		public void ReloadPersistentCoinView()
		{
			_PersistentCoinView.Dispose();
			_CleanList.Remove(_PersistentCoinView);
			_PersistentCoinView = new DBreezeCoinView(_Network, _TestDirectory.FolderName);
			_CleanList.Add(_PersistentCoinView);
		}		
	}
}
