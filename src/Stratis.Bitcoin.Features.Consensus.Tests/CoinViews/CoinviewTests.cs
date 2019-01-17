using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CoinviewTests
    {
        private readonly Network network;
        private readonly DataFolder dataFolder;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly INodeStats nodeStats;
        private readonly DBreezeCoinView dbreezeCoinview;

        private readonly ConcurrentChain concurrentChain;
        private readonly StakeChainStore stakeChainStore;
        private readonly IRewindDataIndexCache rewindDataIndexCache;
        private readonly CachedCoinView cachedCoinView;
        private readonly Random random;

        public CoinviewTests()
        {
            this.network = new StratisMain();
            this.dataFolder = TestBase.CreateDataFolder(this);
            this.dateTimeProvider = new DateTimeProvider();
            this.loggerFactory = new ExtendedLoggerFactory();
            this.nodeStats = new NodeStats(this.dateTimeProvider);

            this.dbreezeCoinview = new DBreezeCoinView(this.network, this.dataFolder, this.dateTimeProvider, this.loggerFactory, this.nodeStats, new DBreezeSerializer(this.network));
            this.dbreezeCoinview.InitializeAsync().GetAwaiter().GetResult();

            this.concurrentChain = new ConcurrentChain(this.network);
            this.stakeChainStore = new StakeChainStore(this.network, this.concurrentChain, this.dbreezeCoinview, this.loggerFactory);
            this.stakeChainStore.LoadAsync().GetAwaiter().GetResult();

            this.rewindDataIndexCache = new RewindDataIndexCache(this.dateTimeProvider, this.network);

            this.cachedCoinView = new CachedCoinView(this.dbreezeCoinview, this.dateTimeProvider, this.loggerFactory, this.nodeStats, this.stakeChainStore, this.rewindDataIndexCache);

            this.rewindDataIndexCache.InitializeAsync(this.concurrentChain.Height, this.cachedCoinView);

            this.random = new Random();

            ChainedHeader newTip = ChainedHeadersHelper.CreateConsecutiveHeaders(1000, this.concurrentChain.Tip, true, null, this.network).Last();
            this.concurrentChain.SetTip(newTip);
        }

        [Fact]
        public async Task TestRewindAsync()
        {
            uint256 tip = await this.cachedCoinView.GetTipHashAsync();
            Assert.Equal(this.concurrentChain.Genesis.HashBlock, tip);

            int currentHeight = 0;

            // Create a lot of new coins.
            List<UnspentOutputs> outputsList = this.CreateOutputsList(currentHeight + 1, 100);
            await this.SaveChangesAsync(outputsList, new List<TxOut[]>(), currentHeight + 1);
            currentHeight++;

            await this.cachedCoinView.FlushAsync(true);

            uint256 tipAfterOriginalCoinsCreation = await this.cachedCoinView.GetTipHashAsync();

            // Collection that will be used as a coinview that we will update in parallel. Needed to verify that actual coinview is ok.
            List<OutPoint> outPoints = this.ConvertToListOfOutputPoints(outputsList);

            // Copy of current state to later rewind and verify against it.
            List<OutPoint> copyOfOriginalOutPoints = new List<OutPoint>(outPoints);

            List<OutPoint> copyAfterHalfOfAdditions = new List<OutPoint>();
            uint256 coinviewTipAfterHalf = null;

            int addChangesTimes = 500;
            // Spend some coins in the next N saves.
            for (int i = 0; i < addChangesTimes; ++i)
            {
                uint256 txId = outPoints[this.random.Next(0, outPoints.Count)].Hash;
                List<OutPoint> txPoints = outPoints.Where(x => x.Hash == txId).ToList();
                this.Shuffle(txPoints);
                List<OutPoint> txPointsToSpend = txPoints.Take(txPoints.Count / 2).ToList();

                // First spend in cached coinview
                FetchCoinsResponse response = await this.cachedCoinView.FetchCoinsAsync(new[] {txId});
                Assert.Single(response.UnspentOutputs);

                UnspentOutputs coins = response.UnspentOutputs[0];
                UnspentOutputs unchangedClone = coins.Clone();

                foreach (OutPoint outPointToSpend in txPointsToSpend)
                    coins.Spend(outPointToSpend.N);

                // Spend from outPoints.
                outPoints.RemoveAll(x => txPointsToSpend.Contains(x));

                // Save coinview
                await this.SaveChangesAsync(new List<UnspentOutputs>() { coins }, new List<TxOut[]>() { unchangedClone.Outputs }, currentHeight + 1);

                currentHeight++;

                if (i == addChangesTimes / 2)
                {
                    copyAfterHalfOfAdditions = new List<OutPoint>(outPoints);
                    coinviewTipAfterHalf = await this.cachedCoinView.GetTipHashAsync();
                }
            }

            await this.ValidateCoinviewIntegrityAsync(outPoints);

            for (int i = 0; i < addChangesTimes; i++)
            {
                await this.cachedCoinView.RewindAsync();

                uint256 currentTip = await this.cachedCoinView.GetTipHashAsync();

                if (currentTip == coinviewTipAfterHalf)
                    await this.ValidateCoinviewIntegrityAsync(copyAfterHalfOfAdditions);
            }

            Assert.Equal(tipAfterOriginalCoinsCreation, await this.cachedCoinView.GetTipHashAsync());

            await this.ValidateCoinviewIntegrityAsync(copyOfOriginalOutPoints);
        }

        private List<OutPoint> ConvertToListOfOutputPoints(List<UnspentOutputs> outputsList)
        {
            var outPoints = new List<OutPoint>();

            foreach (UnspentOutputs output in outputsList)
            {
                for (int i = 0; i < output.Outputs.Length; i++)
                {
                    if (output.Outputs[i] == null)
                        continue;

                    var point = new OutPoint(output.TransactionId, i);

                    outPoints.Add(point);
                }
            }

            return outPoints;
        }

        private UnspentOutputs CreateOutputs(int height, int outputsCount = 20)
        {
            var tx = new Transaction();
            tx.Time = RandomUtils.GetUInt32();

            for (int i = 0; i < outputsCount; i++)
            {
                var money = new Money(this.random.Next(1_000, 1_000_000));

                tx.AddOutput(money, Script.Empty);
            }

            var outputs = new UnspentOutputs((uint)height, tx);
            return outputs;
        }

        private List<UnspentOutputs> CreateOutputsList(int height, int itemsCount = 10)
        {
            var list = new List<UnspentOutputs>();

            for (int i = 0; i < itemsCount; i++)
            {
                list.Add(this.CreateOutputs(height));
            }

            return list;
        }

        private async Task SaveChangesAsync(List<UnspentOutputs> unspent, List<TxOut[]> original, int height)
        {
            ChainedHeader current = this.concurrentChain.Tip.GetAncestor(height);
            ChainedHeader previous = current.Previous;

            await this.cachedCoinView.SaveChangesAsync(unspent, original, previous.HashBlock, current.HashBlock, height);
        }

        private async Task ValidateCoinviewIntegrityAsync(List<OutPoint> expectedAvailableOutPoints)
        {
            foreach (IGrouping<uint256, OutPoint> outPointsGroup in expectedAvailableOutPoints.GroupBy(x => x.Hash))
            {
                uint256 txId = outPointsGroup.Key;
                List<uint> availableIndexes = outPointsGroup.Select(x => x.N).ToList();

                FetchCoinsResponse result = await this.cachedCoinView.FetchCoinsAsync(new[] {txId});
                TxOut[] outputsArray = result.UnspentOutputs[0].Outputs;

                // Check expected coins are present.
                foreach (uint availableIndex in availableIndexes)
                {
                    Assert.NotNull(outputsArray[availableIndex]);
                }

                // Check unexpected coins are not present.
                Assert.Equal(availableIndexes.Count, outputsArray.Count(x => x != null));
            }

            // Verify that snapshot is equal to current state of coinview.
            uint256[] allTxIds = expectedAvailableOutPoints.Select(x => x.Hash).Distinct().ToArray();
            FetchCoinsResponse result2 = await this.cachedCoinView.FetchCoinsAsync(allTxIds);
            List<OutPoint> availableOutPoints = this.ConvertToListOfOutputPoints(result2.UnspentOutputs.ToList());

            Assert.Equal(expectedAvailableOutPoints.Count, availableOutPoints.Count);

            foreach (OutPoint referenceOutPoint in expectedAvailableOutPoints)
            {
                Assert.Contains(referenceOutPoint, availableOutPoints);
            }
        }

        private void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = this.random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
