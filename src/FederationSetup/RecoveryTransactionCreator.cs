using System;
using System.Collections.Generic;
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
    class RecoveryTransactionCreator
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
        /// <returns>A funds recovery transaction that moves funds to the new redeem script.</returns>
        public Transaction CreateFundsRecoveryTransaction(bool isSideChain, Network network, Network counterChainNetwork, string dataDirPath, Script redeemScript, string password)
        {
            // Get the old redeem script from the wallet file.
            PayToMultiSigTemplateParameters multisigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(redeemScript);
            string theChain = isSideChain ? "sidechain" : "mainchain";
            var nodeSettings = new NodeSettings(network, args: new string[] { $"datadir={dataDirPath}", $"redeemscript={redeemScript}", $"-{theChain}" });
            var walletFileStorage = new FileStorage<FederationWallet>(nodeSettings.DataFolder.WalletPath);
            FederationWallet wallet = walletFileStorage.LoadByFileName("multisig_wallet.json");
            Script oldRedeemScript = wallet.MultiSigAddress.RedeemScript;
            PayToMultiSigTemplateParameters oldMultisigParams = PayToMultiSigTemplate.Instance.ExtractScriptPubKeyParameters(oldRedeemScript);

            BitcoinAddress oldMultisigAddress = oldRedeemScript.Hash.GetAddress(network);
            Console.WriteLine($"Old {theChain} P2SH: " + oldMultisigAddress.ScriptPubKey);
            Console.WriteLine($"Old {theChain} multisig address: " + oldMultisigAddress);

            BitcoinAddress newMultisigAddress = redeemScript.Hash.GetAddress(network);
            Console.WriteLine($"New {theChain} P2SH: " + newMultisigAddress.ScriptPubKey);
            Console.WriteLine($"New {theChain} multisig address: " + newMultisigAddress);

            // Create dummy inputs to avoid errors when constructing FederatedPegSettings.
            var extraArgs = new Dictionary<string, string>();
            extraArgs[FederatedPegSettings.FederationIpsParam] = oldMultisigParams.PubKeys.Select(p => "0.0.0.0".ToIPEndPoint(nodeSettings.Network.DefaultPort)).Join(",");
            var privateKey = Key.Parse(wallet.EncryptedSeed, password, network);
            extraArgs[FederatedPegSettings.PublicKeyParam] = privateKey.PubKey.ToHex(network);
            (new TextFileConfiguration(extraArgs.Select(i => $"{i.Key}={i.Value}").ToArray())).MergeInto(nodeSettings.ConfigReader);

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

            // Determine the fee.
            var context = new Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext(Array.Empty<Stratis.Features.FederatedPeg.Wallet.Recipient>().ToList(), password) {
                IsConsolidatingTransaction = true,
                IgnoreVerify = true
            };
            (List<Coin> coins, List<Stratis.Features.FederatedPeg.Wallet.UnspentOutputReference> _) = FederationWalletTransactionHandler.DetermineCoins(walletManager, network, context, federatedPegSettings);
            if (!coins.Any())
                throw new ArgumentException($"There are no coins to recover from the federation wallet on {network}.");
            Money fee = federatedPegSettings.GetWithdrawalTransactionFee(coins.Count());

            var recipients = new List<Stratis.Features.FederatedPeg.Wallet.Recipient>();
            recipients.Add(new Stratis.Features.FederatedPeg.Wallet.Recipient()
            {
                Amount = coins.Sum(c => c.Amount) - fee,
                ScriptPubKey = redeemScript
            });

            var federationWalletTransactionHandler = new FederationWalletTransactionHandler(nodeSettings.LoggerFactory, walletManager, walletFeePolicy, network, federatedPegSettings);

            Transaction tx = federationWalletTransactionHandler.BuildTransaction(context);

            return tx;
        }
    }
}
