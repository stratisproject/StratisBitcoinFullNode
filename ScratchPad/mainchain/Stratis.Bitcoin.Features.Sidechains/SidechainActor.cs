using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.Sidechains
{
	public class SidechainActor : ISidechainActor
	{
		private IWalletManager walletManager;
		private IWalletTransactionHandler walletTransactionHandler;
		private ILogger logger;
		private IBroadcasterManager broadcasterManager;
		private IWatchOnlyWalletManager watchOnlyWalletManager;

		private string walletName = "testnet2";
		private string walletPassword = "Mycats22116$";

		public SidechainActor(ILogger logger, IWalletManager walletManager,
			IWalletTransactionHandler walletTransactionHandler,
			IBroadcasterManager broadcasterManager, IWatchOnlyWalletManager watchOnlyWalletManager)
		{
			this.logger = logger;
			this.walletManager = walletManager;
			this.walletTransactionHandler = walletTransactionHandler;
			this.broadcasterManager = broadcasterManager;
			this.watchOnlyWalletManager = watchOnlyWalletManager;
		}

		//the C++
		//public bool CreateSidechainDeposit(CTransactionRef& tx, std::string& strFail, const uint8_t& nSidechain, const CAmount& nAmount, const CKeyID& keyID)
		public async Task<bool> CreateSidechainDeposit( /*Transaction transaction,*/ /*ref string strFail,*/ int sidechain,
			decimal amount, KeyId keyId)
		{
			//TODO: lock the wallet with a mutex

			var wallet = this.walletManager.GetWalletByName(this.walletName);
			var privateKey = Key.Parse(wallet.EncryptedSeed, this.walletPassword, wallet.Network);
			var seedExtKey = new ExtKey(privateKey, wallet.ChainCode);


			HdAccount highestAcc = null;
			foreach (HdAccount account in this.walletManager.GetAccounts(this.walletName))
			{
				if (highestAcc == null) highestAcc = account;

				if (account.GetSpendableAmount().ConfirmedAmount > highestAcc.GetSpendableAmount().ConfirmedAmount)
					highestAcc = account;
			}

			var walletAccountReference = new WalletAccountReference(this.walletName, highestAcc.Name);
			var unspentOutputs = this.walletManager.GetSpendableTransactionsInAccount(walletAccountReference, 0).ToList();

			var recipient = new Recipient();
			recipient.Amount = new Money(0.42m, MoneyUnit.BTC);
			recipient.ScriptPubKey =
				BitcoinAddress.Create("TNYBX53K9e1SHSy4tBr3o99rYKSKyihvFg", Network.StratisTest).ScriptPubKey;

			//AddCoins
			var balance = unspentOutputs.Sum(t => t.Transaction.Amount);
			var totalToSend = recipient.Amount;
			if (balance < totalToSend)
				throw new WalletException("Not enough funds.");

			Money sum = 0;
			int index = 0;
			var coins = new List<Coin>();
			foreach (var item in unspentOutputs.OrderByDescending(a => a.Transaction.Amount))
			{
				coins.Add(new Coin(item.Transaction.Id, (uint) item.Transaction.Index, item.Transaction.Amount,
					item.Transaction.ScriptPubKey));
				sum += item.Transaction.Amount;
				index++;

				// If threshold is reached and the total value is above the target 
				// then its safe to stop adding UTXOs to the coin list.
				// The primery goal is to reduce the time it takes to build a trx 
				// when the wallet is bloated with UTXOs.
				if (index > 500 && sum > totalToSend)
					break;
			}

			//AddSecrets
			var signingKeys = new HashSet<ISecret>();
			var added = new HashSet<HdAddress>();
			foreach (var unspentOutputsItem in unspentOutputs)
			{
				if (added.Contains(unspentOutputsItem.Address))
					continue;

				var address = unspentOutputsItem.Address;
				ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(address.HdPath));
				BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(wallet.Network);
				signingKeys.Add(addressPrivateKey);
				added.Add(unspentOutputsItem.Address);
			}

			//ChangeAddress
			var changeHdAddress = this.walletManager.GetOrCreateChangeAddress(highestAcc);

			//fees
			var fees = new Money(0.001m, MoneyUnit.BTC);

			//data
			byte[] bytes = Encoding.UTF8.GetBytes("ENIGMA");
			var dataScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
			//});


			var builder = new TransactionBuilder();
			var trans = builder
				.AddCoins(coins)
				.AddKeys(signingKeys.ToArray())
				.Send(recipient.ScriptPubKey, recipient.Amount)
				.SetChange(changeHdAddress.ScriptPubKey)
				.SendFees(fees)
				.Then()
				.Send(dataScript, Money.Zero)
				.BuildTransaction(true);

			bool test = builder.Verify(trans);



			//   byte[] bytes = Encoding.UTF8.GetBytes("ENIGMA");
			//   transaction.Outputs.Add(new TxOut()
			//   {
			//    Value = new Money(0.0001m, MoneyUnit.BTC),
			//    ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
			//});



			//FeeRate feeRate = new FeeRate(new Money(10000, MoneyUnit.Satoshi));
			//WalletAccountReference accountRef = new WalletAccountReference(this.walletName, highestAcc.Name);
			//List<Recipient> recipients = new List<Recipient>();
			//TransactionBuildContext txBuildContext = new TransactionBuildContext(accountRef, recipients);
			//txBuildContext.WalletPassword = this.walletPassword;
			//txBuildContext.OverrideFeeRate = feeRate;
			//txBuildContext.Sign = true;
			//txBuildContext.MinConfirmations = 0;

			//this.walletTransactionHandler.FundTransaction(txBuildContext, transaction);


			//   Money moneyAmount = new Money(amount, MoneyUnit.BTC);

			//var destination = BitcoinAddress.Create("TNYBX53K9e1SHSy4tBr3o99rYKSKyihvFg", Network.StratisTest).ScriptPubKey;
			//   var context = new TransactionBuildContext(
			//    new WalletAccountReference(this.walletName, highestAcc.Name),
			//    new[] { new Recipient { Amount = moneyAmount, ScriptPubKey = destination } }.ToList(),
			//    this.walletPassword)
			//   {
			//    FeeType = FeeType.High,
			//    MinConfirmations = 1,
			//    Shuffle = true,
			//	Sign = true
			//   };

			//   byte[] sideChainAsBytes = BitConverter.GetBytes(sidechain);
			//   if (!BitConverter.IsLittleEndian)
			//    Array.Reverse(sideChainAsBytes);

			//var transactionResult = this.walletTransactionHandler.BuildTransaction(context);


			//this.walletTransactionHandler.FundTransaction(context, transactionResult);

			// User deposit data script
			//CScript dataScript = CScript() << OP_RETURN << nSidechain << ToByteVector(keyID);


			//a constant SIDECHAIN_TEST_KEY is defined in src/sidechain.h
			//in the original C++
			//09c1fbf0ad3047fb825e0bc5911528596b7d7f49
			// TODO: should be based on nSidechain param
			//string SIDECHAIN_PRIVATE_KEY = "VbZTeikkQmhmmFgbZhTuxNKnkE364P1qronEbKWZEUEe2B2dmw9k";
			//Key sidechainKey = Key.Parse(SIDECHAIN_PRIVATE_KEY, Network.StratisTest);
			//var sidechainScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(destination.);

			//a transaction such that it contains an OP_RETURN “xfer out”
			//where the extra data is simply Joe’s sidechain address.

			//transaction.Outputs.Add(new TxOut()
			//{
			//	Value = new Money( amount, MoneyUnit.BTC ),
			//	ScriptPubKey = destination
			//});

			//   HdAccount highestAcc = null;
			//   foreach (HdAccount account in this.walletManager.GetAccounts(this.walletName))
			//   {
			//    if (highestAcc == null) highestAcc = account;

			//    if (account.GetSpendableAmount().ConfirmedAmount > highestAcc.GetSpendableAmount().ConfirmedAmount)
			//	    highestAcc = account;
			//   }

			//   // This fee rate is primarily for regtest, testnet and mainnet have actual estimators that work


			//   //TxIn txIn = transaction.Inputs[0];
			//   //OutPoint outPoint = txIn.PrevOut;

			////Coin coin = new Coin(transaction, 0);

			//   //HdAddress hdAddress = highestAcc.ExternalAddresses.ToArray()[0];
			//   //Wallet.Wallet wallet = this.walletManager.GetWalletByName(this.walletName);
			//   //ISecret secret = wallet.GetExtendedPrivateKeyForAddress(this.walletPassword, hdAddress);

			////transaction.Sign(secret, coin);

			//   this.logger.LogDebug("Trying to broadcast transaction: " + transaction.GetHash());



			var bcResult = await this.broadcasterManager.TryBroadcastAsync(trans).ConfigureAwait(false);
			if (bcResult == Stratis.Bitcoin.Broadcasting.Success.Yes)
			{
				this.logger.LogDebug("Broadcasted transaction: " + trans.GetHash());
			}
			else if (bcResult == Stratis.Bitcoin.Broadcasting.Success.No)
			{
				this.logger.LogDebug("Could not propagate transaction: " + trans.GetHash());
			}
			else if (bcResult == Stratis.Bitcoin.Broadcasting.Success.DontKnow)
			{
				// // wait for propagation
				var waited = TimeSpan.Zero;
				var period = TimeSpan.FromSeconds(1);
				while (TimeSpan.FromSeconds(21) > waited)
				{
					// if broadcasts doesn't contain then success
					var transactionEntry = this.broadcasterManager.GetTransaction(trans.GetHash());
					if (transactionEntry != null && transactionEntry.State == Stratis.Bitcoin.Broadcasting.State.Propagated)
					{
						this.logger.LogDebug("Propagated transaction: " + trans.GetHash());
					}
					await Task.Delay(period).ConfigureAwait(false);
					waited += period;
				}
			}

			this.logger.LogDebug("Uncertain if transaction was propagated: " + trans.GetHash());

			return false;
		}

		public async Task<bool> ExamineCoins()
		{
			var scriptDeposit = BitcoinAddress.Create("TNYBX53K9e1SHSy4tBr3o99rYKSKyihvFg", Network.StratisTest).ScriptPubKey;
			byte[] bytes = Encoding.UTF8.GetBytes("ENIGMA");
			var dataScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);

			string address = scriptDeposit.ToString();
			var watchOnlyWallet = this.watchOnlyWalletManager.GetWatchOnlyWallet();
			var watchedAddress = watchOnlyWallet.WatchedAddresses[address];

			foreach (var watchedAddressTransaction in watchedAddress.Transactions)
			{
				string key = watchedAddressTransaction.Key;
				var transactionData = watchedAddressTransaction.Value;
				var transaction = transactionData.Transaction;
				var hex = transactionData.Hex;
				uint256 blockHash = transactionData.BlockHash;
				uint256 id = transactionData.Id;
				PartialMerkleTree partialMerkleTree = transactionData.MerkleProof;

				//skip transactions we don't like
				if (transaction.GetHash().ToString() != "d90214fd7bdb2aa571f48422f802daa1b96da735d26596b7584340b28ffdb3dd") continue;

				foreach (TxOut txOut in transaction.Outputs)
				{
					if (txOut.ScriptPubKey == scriptDeposit)
					{
						//this is our datascript
						var tran = transaction;
						this.logger.LogInformation(tran.GetHash().ToString());
						Money amount = txOut.Value;
					}
					else if (txOut.ScriptPubKey == dataScript)
					{
						//this is our payment
						var tran = transaction;
						this.logger.LogInformation(tran.GetHash().ToString());

						string script = txOut.ScriptPubKey.ToString();
						byte[] data = txOut.ScriptPubKey.ToBytes();
						string str = Encoding.UTF8.GetString(data).Remove(0, 2);
					}
					else
					{
						//we don't care about this it's probably change
						var tran = transaction;
						this.logger.LogInformation(tran.GetHash().ToString());
					}
				}
			}

			return false;
		}
	}
}
