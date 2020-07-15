using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;

namespace FederationSetup
{
    public class FundsRecoveryTransactionModel
    {
        public Network Network { get; set; }

        public bool IsSideChain { get; set; }

        public PubKey PubKey { get; set; }

        public Script RedeemScript { get; set; }

        public BitcoinAddress oldMultisigAddress { get; set; }

        public BitcoinAddress newMultisigAddress { get; set; }

        public Transaction tx { get; set; }

        public string ChainType => (this.IsSideChain ? "Sidechain" : "Mainchain");

        public void DisplayInfo()
        {
            Console.WriteLine($"Old {this.Network} P2SH: " + this.oldMultisigAddress.ScriptPubKey);
            Console.WriteLine($"Old {this.Network} multisig address: " + this.oldMultisigAddress);

            Console.WriteLine($"New {this.Network} P2SH: " + this.newMultisigAddress.ScriptPubKey);
            Console.WriteLine($"New {this.Network} multisig address: " + this.newMultisigAddress);

            Console.WriteLine($"The transaction has been written to the data directory.");
            Console.WriteLine($"Amount of moving funds: {this.tx.Outputs.Sum(o => o.Value.ToDecimal(MoneyUnit.BTC))}.");
        }

        public string Signature()
        {
            PayToMultiSigTemplateParameters multisigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(this.RedeemScript);

            int index = multisigParams.PubKeys.IndexOf(this.PubKey);

            TransactionSignature signature = PayToMultiSigTemplate.Instance.ExtractScriptSigParameters(this.Network, new Script(tx.Inputs[0].ScriptSig.ToOps().SkipLast(1)))[index];

            return signature.ToString();
        }
    }

    public class RecoveryTransactionCreator
    {
        /// <summary>
        /// Creates a transaction to transfers funds from an old federation to a new federation.
        /// </summary>
        /// <param name="isSideChain">Indicates whether the <paramref name="network"/> is the sidechain.</param>
        /// <param name="network">The network that we are creating the recovery transaction for.</param>
        /// <param name="counterChainNetwork">The counterchain network.</param>
        /// <param name="dataDirPath">The root folder containing the old federation.</param>
        /// <param name="redeemScript">The new redeem script.</param>
        /// <param name="password">The password required to generate transactions using the federation wallet.</param>
        /// <param name="txTime">Any deposits beyond this UTC date will be ignored when selecting coin inputs.</param>
        /// <returns>A funds recovery transaction that moves funds to the new redeem script.</returns>
        public FundsRecoveryTransactionModel CreateFundsRecoveryTransaction(bool isSideChain, Network network, Network counterChainNetwork, string dataDirPath, Script redeemScript, string password, DateTime txTime)
        {
            var model = new FundsRecoveryTransactionModel() { Network = network, IsSideChain = isSideChain, RedeemScript = redeemScript };

            // Get the old redeem script from the wallet file.
            PayToMultiSigTemplateParameters multisigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript);
            string theChain = isSideChain ? "sidechain" : "mainchain";
            var nodeSettings = new NodeSettings(network, args: new string[] { $"datadir={dataDirPath}", $"redeemscript={redeemScript}", $"-{theChain}" });
            var walletFileStorage = new FileStorage<FederationWallet>(nodeSettings.DataFolder.WalletPath);
            FederationWallet wallet = walletFileStorage.LoadByFileName("multisig_wallet.json");
            Script oldRedeemScript = wallet.MultiSigAddress.RedeemScript;
            PayToMultiSigTemplateParameters oldMultisigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(oldRedeemScript);

            model.oldMultisigAddress = oldRedeemScript.Hash.GetAddress(network);
            model.newMultisigAddress = redeemScript.Hash.GetAddress(network);

            // Create dummy inputs to avoid errors when constructing FederatedPegSettings.
            var extraArgs = new Dictionary<string, string>();
            extraArgs[FederatedPegSettings.FederationIpsParam] = oldMultisigParams.PubKeys.Select(p => "0.0.0.0".ToIPEndPoint(nodeSettings.Network.DefaultPort)).Join(",");
            var privateKey = Key.Parse(wallet.EncryptedSeed, password, network);
            extraArgs[FederatedPegSettings.PublicKeyParam] = privateKey.PubKey.ToHex(network);
            (new TextFileConfiguration(extraArgs.Select(i => $"{i.Key}={i.Value}").ToArray())).MergeInto(nodeSettings.ConfigReader);

            model.PubKey = privateKey.PubKey;

            var dBreezeSerializer = new DBreezeSerializer(network.Consensus.ConsensusFactory);
            var blockStore = new BlockRepository(network, nodeSettings.DataFolder, nodeSettings.LoggerFactory, dBreezeSerializer);
            blockStore.Initialize();

            var chain = new ChainRepository(nodeSettings.DataFolder, nodeSettings.LoggerFactory, dBreezeSerializer);
            Block genesisBlock = network.GetGenesis();
            ChainedHeader tip = chain.LoadAsync(new ChainedHeader(genesisBlock.Header, genesisBlock.GetHash(), 0)).GetAwaiter().GetResult();
            var chainIndexer = new ChainIndexer(network, tip);

            var nodeLifetime = new NodeLifetime();
            IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;
            var federatedPegSettings = new FederatedPegSettings(nodeSettings);
            var opReturnDataReader = new OpReturnDataReader(nodeSettings.LoggerFactory, new CounterChainNetworkWrapper(counterChainNetwork));
            var walletFeePolicy = new WalletFeePolicy(nodeSettings);

            var walletManager = new FederationWalletManager(nodeSettings.LoggerFactory, network, chainIndexer, nodeSettings.DataFolder, walletFeePolicy,
                new AsyncProvider(nodeSettings.LoggerFactory, new Signals(nodeSettings.LoggerFactory, new DefaultSubscriptionErrorHandler(nodeSettings.LoggerFactory)), nodeLifetime), nodeLifetime,
                dateTimeProvider, federatedPegSettings, new WithdrawalExtractor(nodeSettings.LoggerFactory, federatedPegSettings, opReturnDataReader, network), blockStore);

            walletManager.Start();
            walletManager.EnableFederationWallet(password);

            if (!walletManager.IsFederationWalletActive())
                throw new ArgumentException($"Could not activate the federation wallet on {network}.");

            // Gets all coins despite passing an empty list of receipients and amounts.
            var context = new Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext(Array.Empty<Stratis.Features.FederatedPeg.Wallet.Recipient>().ToList(), password) {
                IsConsolidatingTransaction = true,
                IgnoreVerify = true
            };

            // Retrieves the unspent outputs in deterministic order.
            List<Stratis.Features.FederatedPeg.Wallet.UnspentOutputReference> coinRefs = walletManager.GetSpendableTransactionsInWallet(context.MinConfirmations).ToList();

            // Exclude coins (deposits) beyond the transaction (switch-over) time!
            coinRefs = coinRefs.Where(r => r.Transaction.CreationTime < txTime).ToList();
            if (!coinRefs.Any())
                throw new ArgumentException($"There are no coins to recover from the federation wallet on {network}.");

            Money fee = federatedPegSettings.GetWithdrawalTransactionFee(coinRefs.Count());

            var builder = new TransactionBuilder(network);
            builder.AddKeys(privateKey);
            builder.AddCoins(coinRefs.Select(c => ScriptCoin.Create(network, c.Transaction.Id, (uint)c.Transaction.Index, c.Transaction.Amount, c.Transaction.ScriptPubKey, oldRedeemScript)));

            // Split the coins into multiple outputs.
            Money amount = coinRefs.Sum(r => r.Transaction.Amount) - fee;
            const int numberOfSplits = 10;
            Money splitAmount = new Money((long)amount / numberOfSplits);
            var recipients = new List<Stratis.Features.FederatedPeg.Wallet.Recipient>();
            for (int i = 0; i < numberOfSplits; i++)
            {
                Money sendAmount = (i != (numberOfSplits - 1)) ? splitAmount : amount - splitAmount * (numberOfSplits - 1);

                builder.Send(redeemScript.PaymentScript, sendAmount);
            }

            builder.SetTimeStamp((uint)(new DateTimeOffset(txTime)).ToUnixTimeSeconds());
            builder.CoinSelector = new DeterministicCoinSelector();
            builder.SendFees(fee);

            model.tx = builder.BuildTransaction(true);

            File.WriteAllText(Path.Combine(dataDirPath, $"{network.Name}_{model.PubKey.ToHex(network).Substring(0, 8)}.hex"), model.tx.ToHex(network));

            // Merge our transaction with other transactions which have been placed in the data folder.
            Transaction oldTransaction = model.tx;
            string namePattern = $"{network.Name}_*.hex";
            int sigCount = 1;
            foreach (string fileName in Directory.EnumerateFiles(dataDirPath, namePattern))
            {
                Transaction incomingPartialTransaction = network.CreateTransaction(File.ReadAllText(fileName));

                // Don't merge with self.
                if (incomingPartialTransaction.GetHash() == oldTransaction.GetHash())
                    continue;

                // Transaction times must match.
                if (incomingPartialTransaction is PosTransaction && incomingPartialTransaction.Time != model.tx.Time)
                {
                    Console.WriteLine($"The locally generated transaction is time-stamped differently from the transaction contained in '{fileName}'. The imported signature can't be used.");
                    continue;
                }

                // Combine signatures.
                Transaction newTransaction = SigningUtils.CheckTemplateAndCombineSignatures(builder, model.tx, new[] { incomingPartialTransaction });

                if (oldTransaction.GetHash() == newTransaction.GetHash())
                {
                    Console.WriteLine($"The locally generated transaction is not similar to '{fileName}'. The imported signature can't be used.");
                    continue;
                }

                model.tx = newTransaction;
                sigCount++;
            }

            Console.WriteLine($"{sigCount} of {multisigParams.SignatureCount} signatures collected for {network.Name}.");

            if (sigCount >= multisigParams.SignatureCount)
            {
                if (builder.Verify(model.tx))
                {
                    // Write the transaction to file.
                    File.WriteAllText(Path.Combine(dataDirPath, $"{(txTime > DateTime.Now ? "Preliminary " : "")}{network.Name}Recovery.txt"), model.tx.ToHex(network));
                }
                else
                    Console.WriteLine("Could not verify the transaction.");
            }

            // Stop the wallet manager to release the database folder.
            nodeLifetime.StopApplication();
            walletManager.Stop();

            return model;
        }
    }
}
