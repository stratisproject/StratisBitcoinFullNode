using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.Sidechains.Controllers
{
	[Route("api/[controller]")]
	public class SidechainsController : Controller
	{
		private Network network;
		private IWalletManager walletManager;
		private IWalletTransactionHandler walletTransactionHandler;
		private ILogger logger;
		private IBroadcasterManager broadcasterManager;
		private IWatchOnlyWalletManager watchOnlyWalletManager;

		public SidechainsController(ILoggerFactory loggerFactory, IWalletManager walletManager,
			IWalletTransactionHandler walletTransactionHandler,
			IBroadcasterManager broadcasterManager, Network network, IWatchOnlyWalletManager watchOnlyWalletManager)
		{
			this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
			this.walletManager = walletManager;
			this.walletTransactionHandler = walletTransactionHandler;
			this.broadcasterManager = broadcasterManager;
			this.network = network;
			this.watchOnlyWalletManager = watchOnlyWalletManager;
		}

		[Route("deposit")]
		[HttpGet]
		public async Task<IActionResult> DepositAsync()
		{
			//This is the sidechain deposit address and is generated
			//on the sidechain side and given to the depositor who
			//wants to move coins from the mainchain to the sidechain.
			//this is like a normal deposit except the address comes
			//from another blockchain (the sidechain).
			//TCu5N64FYQBJG29dxqXurCDAAVzVNjfZ84 = random regtest address i had lying around
			var bitcoinAddress = new BitcoinPubKeyAddress("TCu5N64FYQBJG29dxqXurCDAAVzVNjfZ84", this.network);
			var keyId = bitcoinAddress.Hash;

			//we only have one sidechain
			int sidechain = 1;

			var transaction = new Transaction();
			string failMessage = string.Empty;
			decimal amount = 0.01m;

			SidechainActor actor = new SidechainActor(this.logger, this.walletManager, this.walletTransactionHandler,
				this.broadcasterManager, this.watchOnlyWalletManager);
			bool result = await actor.CreateSidechainDeposit( /*transaction,*/ /*ref failMessage*/ sidechain, amount, keyId)
				.ConfigureAwait(false);

			return this.Ok();
		}

		[Route("examine")]
		[HttpGet]
		public async Task<IActionResult> ExamineCoinsAsync()
		{
			SidechainActor actor = new SidechainActor(this.logger, this.walletManager, this.walletTransactionHandler,
				this.broadcasterManager, this.watchOnlyWalletManager);
			bool result = await actor.ExamineCoins();

			return this.Ok();
		}
	}
}