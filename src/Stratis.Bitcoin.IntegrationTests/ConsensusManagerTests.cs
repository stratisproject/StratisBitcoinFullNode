using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private readonly Network network;

        private const string walletName = "mywallet";
        private const string walletPassword = "123456";

        public ConsensusManagerTests()
        {
            this.network = KnownNetworks.StratisRegTest;
        }

        [Fact]
        public void ForkScenario()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.network);
                var minerB = builder.CreateStratisPowNode(this.network);
                var syncer = builder.CreateStratisPowNode(this.network);

                builder.StartAll();

                minerA.NotInIBD().WithWallet();
                minerB.NotInIBD().WithWallet();
                syncer.NotInIBD().WithWallet();

                HdAddress ususedAddress = minerA.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, "account 0"));
                Features.Wallet.Wallet wallet = minerA.FullNode.WalletManager().GetWalletByName(walletName);
                Key key = wallet.GetExtendedPrivateKeyForAddress(walletPassword, ususedAddress).PrivateKey;

                minerA.SetDummyMinerSecret(new BitcoinSecret(key, minerA.FullNode.Network));
                minerB.SetDummyMinerSecret(new BitcoinSecret(key, minerB.FullNode.Network));
                syncer.SetDummyMinerSecret(new BitcoinSecret(key, syncer.FullNode.Network));

                // MinerA mines to height 5.
                minerA.GenerateStratisWithMiner(5);

                // Miner A mines to height 4 so that we can inject bad block at height 5.
                minerB.GenerateStratisWithMiner(4);
                minerB.GenerateBlockManually();
            }
        }
    }
}
