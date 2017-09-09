using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    /// <summary>
    /// Unit tests for memory pool manager.
    /// </summary>
    public class MempoolManagerTest
    {
        /// <summary>
        /// Creates a mock MempoolManager class.
        /// </summary>
        private MempoolManager Manager
        {
            get
            {
                Mock<IDateTimeProvider> mockDateTime = new Mock<IDateTimeProvider>();
                ILoggerFactory loggerFactory = new Mock<ILoggerFactory>().Object;
                MempoolSettings settings = new MempoolSettings(i => { });

                //TODO: Need to extract interface out of TxMempool for Manager/Orphans methods - for mocking
                BlockPolicyEstimator blockPolicyEstimator = new BlockPolicyEstimator(new FeeRate(1000), settings, loggerFactory);
                TxMempool mempool = new TxMempool(new FeeRate(1000), mockDateTime.Object, blockPolicyEstimator, loggerFactory);

                Mock<TxMempool> mockTxMempool = new Mock<TxMempool>();
                Mock<IMempoolValidator> mockValidator = new Mock<IMempoolValidator>();

                Mock<IMempoolPersistence> mockPersist = new Mock<IMempoolPersistence>();
                Mock<CoinView> mockCoinView = new Mock<CoinView>();

                return new MempoolManager(new MempoolAsyncLock(), mempool, mockValidator.Object, null, mockDateTime.Object, settings, mockPersist.Object, mockCoinView.Object, loggerFactory);
            }
        }

        [Fact]
        public async Task AddMempoolEntriesToMempool_WithNull_ThrowsNoException()
        {
            MempoolManager manager = this.Manager;
            Assert.NotNull(manager);

            await manager.AddMempoolEntriesToMempool(null);
        }

        [Fact]
        public async Task AddMempoolEntriesToMempool_WithExpiredTx_PurgesTx()
        {
            MempoolManager manager = this.Manager;
            Assert.NotNull(manager);

            //TODO: Implement test
            await manager.AddMempoolEntriesToMempool(null);
        }

    }
}
