using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class ChainIndexerRangeQueryTests
    {
        private readonly SmartContractsPoATest network;

        public ChainIndexerRangeQueryTests()
        {
            this.network = new SmartContractsPoATest();
        }

        [Theory]
        [InlineData(10, 0, 9)]
        [InlineData(10, 0, null)]
        [InlineData(10, 1, 9)]
        [InlineData(10, 1, null)]
        [InlineData(10, 0, 0)]
        [InlineData(10, 9, 9)]
        [InlineData(10, 9, null)]
        public void Query_Range_Success(int chainLength, int start, int? end)
        {
            var chainIndexer = new ChainIndexer(this.network);

            var chain = this.CreateChain(chainIndexer.Genesis, chainLength);

            chainIndexer.Initialize(chain.Last());

            var query = new ChainIndexerRangeQuery(chainIndexer);

            var result = query.EnumerateRange(start, end).ToList();

            for (var i = 0; i < result.Count; i++)
            {
                Assert.Equal(chain[start + i], result[i]);
            }

            Assert.Equal((end ?? (chainLength - 1)) - start + 1, result.Count);
        }

        [Fact]
        public void Query_Range_From_Header_Null_Empty()
        {
            var chainIndexer = new ChainIndexer(this.network);

            var chain = this.CreateChain(chainIndexer.Genesis, 1);

            chainIndexer.Initialize(chain.Last());

            var query = new ChainIndexerRangeQuery(chainIndexer);

            // Chain length is only the genesis block, so this should fail.
            var result = query.EnumerateRange(1, 10);

            Assert.Empty(result);
        }

        [Fact]
        public void Query_Range_To_Header_Null()
        {
            var chainIndexer = new ChainIndexer(this.network);

            var chain = this.CreateChain(chainIndexer.Genesis, 5);

            chainIndexer.Initialize(chain.Last());

            var query = new ChainIndexerRangeQuery(chainIndexer);

            // Chain length is only 5, so this should return all elements.
            var result = query.EnumerateRange(0, 10);

            Assert.Equal(5, result.Count());
        }

        [Fact]
        public void Query_Range_Negative_Count_Empty()
        {
            var chainIndexer = new ChainIndexer(this.network);

            var query = new ChainIndexerRangeQuery(chainIndexer);

            // Must call ToList here or nothing gets enumerated
            Assert.Empty(query.EnumerateRange(100, 10));
        }

        /// <summary>
        /// Creates a chain of headers of the specified length, starting from the specified header.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private ChainedHeader[] CreateChain(ChainedHeader start, int length)
        {
            var prevBlockHeader = start;
            var headers = new ChainedHeader[length];

            headers[0] = start;

            for (var i = 1; i < length; i++)
            {
                var bh = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
                bh.HashPrevBlock = prevBlockHeader.HashBlock;

                var header = new ChainedHeader(
                    bh,
                    new uint256((ulong) i + 1UL),
                    prevBlockHeader
                );

                prevBlockHeader = header;


                headers[i] = header;
            }

            return headers;
        }
    }
}