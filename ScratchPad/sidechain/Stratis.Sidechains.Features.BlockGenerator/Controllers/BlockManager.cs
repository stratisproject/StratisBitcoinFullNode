using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using System.Linq;

namespace Stratis.Sidechains.Features.BlockGenerator.Controllers
{
	public class BlockManager
	{
		private FullNode fullNode;
		private Network network;
		private IWalletManager walletManager;

		public BlockManager(IWalletManager walletManager, Network network, FullNode fullNode)
		{
			this.walletManager = walletManager;
			this.network = network;
			this.fullNode = fullNode;
		}

		public async Task<bool> BlockGenerate(int numberOfBlocks)
		{
			var wallet = this.walletManager;
			var w = wallet.GetWalletsNames().FirstOrDefault();
			if (w == null)
				throw new Exception("No wallet found");
			var acc = wallet.GetAccounts(w).FirstOrDefault();
			var account = new WalletAccountReference(w, acc.Name);
			var address = wallet.GetUnusedAddress(account);

			try
			{
				PowMining powMining = this.fullNode.NodeService<PowMining>();

				if (this.network == Network.Main || this.network == Network.TestNet || this.network == Network.RegTest)
				{
					// Bitcoin PoW
					powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)numberOfBlocks, int.MaxValue);
				}

				if (this.network == Network.StratisMain || this.network == Network.StratisTest || this.network == Network.StratisRegTest ||
				this.network == Network.SidechainMain || this.network == Network.SidechainTest || this.network == Network.SidechainRegTest)
				{
					// Stratis PoW
					powMining.GenerateBlocks(new ReserveScript { reserveSfullNodecript = address.ScriptPubKey }, (ulong)numberOfBlocks, int.MaxValue);
				}

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}