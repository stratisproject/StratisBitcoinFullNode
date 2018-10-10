using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    /// <summary>
    /// Unit tests for memory pool manager.
    /// </summary>
    public class MempoolManagerTest
    {
        /// <summary>
        /// Expiry time to use for test in hours.
        /// </summary>
        private const int MempoolExpiry = MempoolValidator.DefaultMempoolExpiry;

        /// <summary>
        /// Creates a mock MempoolManager class for testing tx expiry.
        /// </summary>
        private MempoolManager TxExpiryManager
        {
            get
            {
                IDateTimeProvider dateTime = new DateTimeProvider();

                var mockLoggerFactory = new Mock<ILoggerFactory>();
                mockLoggerFactory.Setup(i => i.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
                ILoggerFactory loggerFactory = mockLoggerFactory.Object;

                var settings = new MempoolSettings(NodeSettings.Default(KnownNetworks.TestNet))
                {
                    MempoolExpiry = MempoolExpiry
                };
                NodeSettings nodeSettings = NodeSettings.Default(KnownNetworks.TestNet);

                var blockPolicyEstimator = new BlockPolicyEstimator(settings, loggerFactory, nodeSettings);
                var mempool = new TxMempool(dateTime, blockPolicyEstimator, loggerFactory, nodeSettings);

                var mockTxMempool = new Mock<TxMempool>();
                var mockValidator = new Mock<IMempoolValidator>();
                mockValidator.Setup(i =>
                    i.AcceptToMemoryPoolWithTime(It.IsAny<MempoolValidationState>(), It.IsAny<Transaction>()))
                        .ReturnsAsync((MempoolValidationState state, Transaction tx) =>
                        {
                            var consensusOptions = new ConsensusOptions();
                            mempool.MapTx.Add(new TxMempoolEntry(tx, Money.Zero, 0, 0, 0, Money.Zero, false, 0, null, consensusOptions));
                            return true;
                        }
                        );

                var mockPersist = new Mock<IMempoolPersistence>();
                var mockCoinView = new Mock<ICoinView>();

                return new MempoolManager(new MempoolSchedulerLock(), mempool, mockValidator.Object, dateTime, settings, mockPersist.Object, mockCoinView.Object, loggerFactory, nodeSettings.Network);
            }
        }

        [Fact]
        public async Task AddMempoolEntriesToMempool_WithNull_ThrowsNoExceptionAsync()
        {
            MempoolManager manager = this.TxExpiryManager;
            Assert.NotNull(manager);

            await manager.AddMempoolEntriesToMempoolAsync(null);
        }

        [Fact]
        public async Task AddMempoolEntriesToMempool_WithExpiredTx_PurgesTxAsync()
        {
            MempoolManager manager = this.TxExpiryManager;
            Assert.NotNull(manager);

            long expiryInSeconds = MempoolValidator.DefaultMempoolExpiry * 60 * 60;

            // tx with expiry twice as long as default expiry
            var txs = new List<MempoolPersistenceEntry>
            {
                    new MempoolPersistenceEntry
                    {
                    Tx =new Transaction(),
                    Time = manager.DateTimeProvider.GetTime() - expiryInSeconds*2
                    }
            };
            await manager.AddMempoolEntriesToMempoolAsync(txs);
            long entries = await manager.MempoolSize();

            // Should not add it because it's expired
            Assert.Equal(0L, entries);
        }

        [Fact]
        public async Task AddMempoolEntriesToMempool_WithUnexpiredTx_AddsTxAsync()
        {
            MempoolManager manager = this.TxExpiryManager;
            Assert.NotNull(manager);

            long expiryInSeconds = MempoolValidator.DefaultMempoolExpiry * 60 * 60;

            // tx with expiry half as long as default expiry
            var txs = new List<MempoolPersistenceEntry>
            {
                    new MempoolPersistenceEntry
                    {
                    Tx =new Transaction(),
                    Time = manager.DateTimeProvider.GetTime() - expiryInSeconds/2
                    }
            };
            await manager.AddMempoolEntriesToMempoolAsync(txs);
            long entries = await manager.MempoolSize();

            // Not expired so should have been added
            Assert.Equal(1L, entries);
        }
    }
}
