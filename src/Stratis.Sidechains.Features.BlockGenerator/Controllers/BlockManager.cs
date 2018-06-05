using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using System.Linq;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Sidechains.Features.BlockchainGeneration;

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

		public bool BlockGenerate(int numberOfBlocks)
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
				PowMining powMining = this.fullNode.NodeService<IPowMining>() as PowMining;

				if (this.network == Network.Main || this.network == Network.TestNet || this.network == Network.RegTest)
				{
					// Bitcoin PoW
					powMining.GenerateBlocks(new ReserveScript(address.Pubkey), (ulong)numberOfBlocks, int.MaxValue);
				}

				if (this.network == SidechainNetwork.SidechainMain || this.network == SidechainNetwork.SidechainTest || this.network == SidechainNetwork.SidechainRegTest)
				{
					// Stratis PoW
					powMining.GenerateBlocks(new ReserveScript { ReserveFullNodeScript = address.ScriptPubKey }, (ulong) numberOfBlocks, int.MaxValue);
				}

				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}
	}
}