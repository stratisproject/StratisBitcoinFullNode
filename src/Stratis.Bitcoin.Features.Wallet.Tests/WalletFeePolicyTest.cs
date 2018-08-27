using System;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests
{
    public class WalletFeePolicyTest : LogsTestBase
    {
        private const int blocksInChainCount = 3;
        private const int notEmptyTxCount = 10;

        /// <summary>
        /// Tests GetFeeRate return min relay fee rate if blocks are empty.
        /// Mock out to return block with only 1 tx.
        /// </summary>
        [Fact]
        public void GetFeeRateWithEmptyBlocksReturnsMinRelayFee()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = NodeSettings.Default();

            var chain = this.GetMockChain();

            var blockRepoMock = new Mock<IBlockRepository>();
            var block = this.Network.CreateBlock(); 
            block.AddTransaction(new Transaction());
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();
            block.Header.BlockTime = DateTimeOffset.Now;
            blockRepoMock.Setup(m => m.GetAsync(It.IsAny<uint256>()))
                .ReturnsAsync(block);

            WalletFeePolicy walletFeePolicy = new WalletFeePolicy(nodeSettings, chain, blockRepoMock.Object, this.LoggerFactory.Object);
            Assert.Equal(nodeSettings.MinRelayTxFeeRate, walletFeePolicy.GetFeeRate(20));
        }

        /// <summary>
        /// Tests GetFeeRate return fallback fee rate if blocks are not empty.
        /// Mock out to return block with 10 tx.
        /// </summary>
        [Fact]
        public void GetFeeRateWithNotEmptyBlocksReturnsFallbackFee()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var nodeSettings = NodeSettings.Default();

            var chain = this.GetMockChain();

            var blockRepoMock = new Mock<IBlockRepository>();
            var block = this.Network.CreateBlock();
            for (int i = 0; i < notEmptyTxCount; i++)
            {
                block.AddTransaction(new Transaction());
            }
            block.UpdateMerkleRoot();
            block.Header.HashPrevBlock = chain.Genesis.HashBlock;
            block.Header.Nonce = RandomUtils.GetUInt32();
            block.Header.BlockTime = DateTimeOffset.Now;
            blockRepoMock.Setup(m => m.GetAsync(It.IsAny<uint256>()))
                .ReturnsAsync(block);        

            WalletFeePolicy walletFeePolicy = new WalletFeePolicy(nodeSettings, chain, blockRepoMock.Object, this.LoggerFactory.Object);
            Assert.Equal(nodeSettings.FallbackTxFeeRate, walletFeePolicy.GetFeeRate(20));
        }

        private ConcurrentChain GetMockChain()
        {
            var chain = new ConcurrentChain(KnownNetworks.StratisMain);

            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blocksInChainCount; i++)
            {
                var block = this.Network.CreateBlock();
                block.AddTransaction(new Transaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = RandomUtils.GetUInt32();
                block.Header.BlockTime = DateTimeOffset.Now;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }
    }
}
