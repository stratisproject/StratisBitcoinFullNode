using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Features.FederatedPeg.Wallet;

namespace FederationSetup
{
    class RecoveryTransactionCreator
    {
        public Transaction CreateFundsRecoveryTransaction(Network network, Network counterChainNetwork, string dataDirPath, Script oldRedeemScript, Script newRedeemScript)
        {
            Transaction tx = network.CreateTransaction();
            var nodeSettings = new NodeSettings(network, args: new string[] { $"datadir={dataDirPath}", $"redeemscript={oldRedeemScript}" });

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
            OpReturnDataReader opReturnDataReader = new OpReturnDataReader(nodeSettings.LoggerFactory, new CounterChainNetworkWrapper(counterChainNetwork));

            FederationWalletManager walletManager = new FederationWalletManager(nodeSettings.LoggerFactory, network, chainIndexer, nodeSettings.DataFolder, new WalletFeePolicy(nodeSettings),
                new AsyncProvider(nodeSettings.LoggerFactory, new Signals(nodeSettings.LoggerFactory, new DefaultSubscriptionErrorHandler(nodeSettings.LoggerFactory)), nodeLifetime), nodeLifetime,
                dateTimeProvider, federatedPegSettings, new WithdrawalExtractor(nodeSettings.LoggerFactory, federatedPegSettings, opReturnDataReader, network), blockStore);

            walletManager.Start();


            return tx;
        }
    }
}
