using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            if (this.tx != null)
            {
                Console.WriteLine($"The transaction has been written to the data directory.");
                Console.WriteLine($"Amount of moving funds: {this.tx.Outputs.Sum(o => o.Value.ToDecimal(MoneyUnit.BTC))}.");
            }
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
        /// <param name="multisigParams">Parameters related to the new redeem script.</param>
        /// <param name="password">The password required to generate transactions using the federation wallet.</param>
        /// <param name="txTime">Any deposits beyond this UTC date will be ignored when selecting coin inputs.</param>
        /// <param name="newFormat">Set to <c>true</c> to send the funds to a P2SH that is based on the new redeem script format.</param>
        /// <param name="burn">Set to <c>true</c> to sent the funds to an OP_RETURN containing the target multisig address.</param>
        /// <returns>A funds recovery transaction that moves funds to the new redeem script.</returns>
        public FundsRecoveryTransactionModel CreateFundsRecoveryTransaction(bool isSideChain, Network network, Network counterChainNetwork, string dataDirPath, 
            PayToMultiSigTemplateParameters multisigParams, string password, DateTime txTime, bool newFormat = false, bool burn = false)
        {
            Script redeemScript = NewRedeemScript(multisigParams, newFormat);

            var model = new FundsRecoveryTransactionModel() { Network = network, IsSideChain = isSideChain, RedeemScript = redeemScript };

            string theChain = isSideChain ? "sidechain" : "mainchain";
            var nodeSettings = new NodeSettings(network, args: new string[] { $"datadir={dataDirPath}", $"-{theChain}" });

            // Get the old redeem script from the wallet file.
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
            extraArgs[FederatedPegSettings.RedeemScriptParam] = oldRedeemScript.ToString();
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

            // Retrieves the unspent outputs in deterministic order.
            List<Stratis.Features.FederatedPeg.Wallet.UnspentOutputReference> coinRefs = walletManager.GetSpendableTransactionsInWallet().ToList();

            // Exclude coins (deposits) beyond the transaction (switch-over) time!
            coinRefs = coinRefs.Where(r => r.Transaction.CreationTime < txTime).ToList();
            if (!coinRefs.Any())
            {
                Console.WriteLine($"There are no coins to recover from the federation wallet on {network}.");
            }
            else
            {
                Money fee = federatedPegSettings.GetWithdrawalTransactionFee(coinRefs.Count());

                var builder = new TransactionBuilder(network);
                builder.AddKeys(privateKey);
                builder.AddCoins(coinRefs.Select(c => ScriptCoin.Create(network, c.Transaction.Id, (uint)c.Transaction.Index, c.Transaction.Amount, c.Transaction.ScriptPubKey, oldRedeemScript)));

                Money amount = coinRefs.Sum(r => r.Transaction.Amount) - fee;

                if (!burn)
                {
                    // Split the coins into multiple outputs.
                    const int numberOfSplits = 10;
                    Money splitAmount = new Money((long)amount / numberOfSplits);
                    Script recipient = redeemScript.PaymentScript;

                    for (int i = 0; i < numberOfSplits; i++)
                    {
                        Money sendAmount = (i != (numberOfSplits - 1)) ? splitAmount : amount - splitAmount * (numberOfSplits - 1);

                        builder.Send(recipient, sendAmount);
                    }
                }
                else
                {
                    // We don't have the STRAX network classes but we need a STRAX address.
                    // These Base58 prefixes will aid in synthesizing the required format.
                    const byte straxMainScriptAddressPrefix = 140;
                    const byte straxTestScriptAddressPrefix = 127;
                    const byte straxRegTestScriptAddressPrefix = 127;

                    // We only have the prefixes for the Stratis -> Strax path.
                    Guard.Assert(network.Name.StartsWith("Stratis"));

                    // Determine the required prefix determining on the type of network.
                    byte addressPrefix = straxMainScriptAddressPrefix;
                    if (network.IsRegTest())
                        addressPrefix = straxRegTestScriptAddressPrefix;
                    else if (network.IsTest())
                        addressPrefix = straxTestScriptAddressPrefix;

                    // Synthesize the multisig address with a STRAX prefix.
                    byte[] temp = network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS];
                    network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { addressPrefix };
                    TxDestination txDestination = redeemScript.PaymentScript.GetDestination(network);
                    model.newMultisigAddress = txDestination.GetAddress(network);
                    network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = temp;

                    // Add it to the OP_RETURN.
                    byte[] bytes = Encoding.UTF8.GetBytes(model.newMultisigAddress.ToString());
                    Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
                    builder.Send(opReturnScript, amount);
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

                Console.WriteLine($"{sigCount} of {oldMultisigParams.SignatureCount} signatures collected for {network.Name}.");

                if (sigCount >= oldMultisigParams.SignatureCount)
                {
                    if (builder.Verify(model.tx))
                    {
                        // Write the transaction to file.
                        File.WriteAllText(Path.Combine(dataDirPath, $"{(txTime > DateTime.Now ? "Preliminary " : "")}{network.Name}Recovery.txt"), model.tx.ToHex(network));
                    }
                    else
                        Console.WriteLine("Could not verify the transaction.");
                }
            }

            // Stop the wallet manager to release the database folder.
            nodeLifetime.StopApplication();
            walletManager.Stop();

            return model;
        }

        private Script NewRedeemScript(PayToMultiSigTemplateParameters para, bool newFormat)
        {
            Script script;

            if (newFormat)
            {
                // Determine the federation id.
                byte[] federationId = para.PubKeys[0].ToBytes();
                for (int i = 1; i < para.PubKeys.Length; i++)
                {
                    byte[] nextFederationId = para.PubKeys[i].ToBytes();

                    for (int j = 0; j < federationId.Length; j++)
                    {
                        federationId[j] ^= nextFederationId[j];
                    }
                }

                script = new Script(Op.GetPushOp(federationId), OpcodeType.OP_NOP9 /* OP_FEDERATION */, OpcodeType.OP_CHECKMULTISIG);
            }
            else
            {
                script = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(para.SignatureCount, para.PubKeys);
            }

            return script;
        }
    }
}
