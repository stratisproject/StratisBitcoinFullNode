using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class ChainTests
    {
        readonly Network network;
        readonly Network networkTest;

        public ChainTests()
        {
            this.network = KnownNetworks.Main;
            this.networkTest = KnownNetworks.TestNet;
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanSaveChain()
        {
            var chain = new ChainIndexer(this.network);

            this.AppendBlock(chain);
            this.AppendBlock(chain);

            ChainedHeader fork = this.AppendBlock(chain);
            this.AppendBlock(chain);

            var chain2 = new ChainIndexer(this.network).Load(chain.ToBytes());
            Assert.True(chain.Tip.HashBlock == chain2.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void IncompleteScriptDoesNotHang()
        {
            new Script(new byte[] { 0x4d }).ToString();
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanParseRandomScripts()
        {
            for (int i = 0; i < 600; i++)
            {
                byte[] bytes = RandomUtils.GetBytes(120);
                new Script(bytes).ToString();
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanLoadAndSaveConcurrentChain()
        {
            var cchain = new ChainIndexer(this.network);
            var chain = new ChainIndexer(this.network);

            this.AddBlock(chain);
            this.AddBlock(chain);
            this.AddBlock(chain);

            cchain.SetTip(chain.Tip);

            byte[] bytes = cchain.ToBytes();
            cchain = new ChainIndexer(this.network);
            cchain.Load(bytes);

            Assert.Equal(cchain.Tip, chain.Tip);
            Assert.NotNull(cchain.GetHeader(0));

            cchain = new ChainIndexer(this.networkTest);
            cchain.Load(cchain.ToBytes());
            Assert.NotNull(cchain.GetHeader(0));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildConcurrentChain()
        {
            var cchain = new ChainIndexer(this.network);
            var chain = new ChainIndexer(this.network);

            ChainedHeader b0 = cchain.Tip;
            Assert.Equal(cchain.Tip, chain.Tip);

            ChainedHeader b1 = this.AddBlock(chain);
            ChainedHeader b2 = this.AddBlock(chain);
            this.AddBlock(chain);
            this.AddBlock(chain);
            ChainedHeader b5 = this.AddBlock(chain);

            Assert.Equal(cchain.SetTip(chain.Tip), b0);
            Assert.Equal(cchain.Tip, chain.Tip);

            Assert.Equal(cchain.GetHeader(5), chain.Tip);
            Assert.Equal(cchain.GetHeader(b5.HashBlock), chain.Tip);

            Assert.Equal(cchain.SetTip(b1), b1);
            Assert.Null(cchain.GetHeader(b5.HashBlock));
            Assert.Null(cchain.GetHeader(b2.HashBlock));

            Assert.Equal(cchain.SetTip(b5), b1);
            Assert.Equal(cchain.GetHeader(b5.HashBlock), chain.Tip);

            chain.SetTip(b2);
            this.AddBlock(chain);
            this.AddBlock(chain);
            ChainedHeader b5b = this.AddBlock(chain);
            ChainedHeader b6b = this.AddBlock(chain);

            Assert.Equal(cchain.SetTip(b6b), b2);

            Assert.Null(cchain.GetHeader(b5.HashBlock));
            Assert.Equal(cchain.GetHeader(b2.HashBlock), b2);
            Assert.Equal(cchain.GetHeader(6), b6b);
            Assert.Equal(cchain.GetHeader(5), b5b);
        }

        private ChainedHeader AddBlock(ChainIndexer chainIndexer)
        {
            BlockHeader header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            header.Nonce = RandomUtils.GetUInt32();
            header.HashPrevBlock = chainIndexer.Tip.HashBlock;
            chainIndexer.SetTip(header);
            return chainIndexer.GetHeader(header.GetHash());
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildChain()
        {
            var chain = new ChainIndexer(this.network);

            this.AppendBlock(chain);
            this.AppendBlock(chain);
            this.AppendBlock(chain);
            ChainedHeader b = this.AppendBlock(chain);
            Assert.Equal(4, chain.Height);
            Assert.Equal(4, b.Height);
            Assert.Equal(b.HashBlock, chain.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCalculateDifficulty()
        {
            var main = new ChainIndexer(this.network).Load(this.LoadMainChain());
            // The state of the line separators may be affected by copy operations - so do an environment independent line split...
            string[] histories = File.ReadAllText(TestDataLocations.GetFileFromDataFolder("targethistory.csv")).Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string history in histories)
            {
                int height = int.Parse(history.Split(',')[0]);
                var expectedTarget = new Target(new BouncyCastle.Math.BigInteger(history.Split(',')[1], 10));

                BlockHeader block = main.GetHeader(height).Header;

                Assert.Equal(expectedTarget, block.Bits);
                Target target = main.GetHeader(height).GetWorkRequired(network);
                Assert.Equal(expectedTarget, target);
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanValidateChain()
        {
            var main = new ChainIndexer(this.network).Load(this.LoadMainChain());
            foreach (ChainedHeader h in main.EnumerateToTip(main.Genesis))
            {
                Assert.True(h.Validate(this.network));
            }
        }

        private byte[] LoadMainChain()
        {
            if (!File.Exists("MainChain1.dat"))
            {
                var client = new HttpClient();
                byte[] bytes = client.GetByteArrayAsync("https://aois.blob.core.windows.net/public/MainChain1.dat").GetAwaiter().GetResult();
                File.WriteAllBytes("MainChain1.dat", bytes);
            }
            return File.ReadAllBytes("MainChain1.dat");
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanEnumerateAfterChainedBlock()
        {
            var chain = new ChainIndexer(this.network);

            this.AppendBlock(chain);
            ChainedHeader a = this.AppendBlock(chain);
            ChainedHeader b = this.AppendBlock(chain);
            ChainedHeader c = this.AppendBlock(chain);

            Assert.True(chain.EnumerateAfter(a).SequenceEqual(new[] { b, c }));

            ChainedHeader d = this.AppendBlock(chain);

            IEnumerator<ChainedHeader> enumerator = chain.EnumerateAfter(b).GetEnumerator();
            enumerator.MoveNext();
            Assert.True(enumerator.Current == c);

            chain.Initialize(b);
            ChainedHeader cc = this.AppendBlock(chain);
            ChainedHeader dd = this.AppendBlock(chain);

            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildChain2()
        {
            ChainIndexer chainIndexer = this.CreateChain(10);
            this.AppendBlock(chainIndexer);
            this.AppendBlock(chainIndexer);
            this.AppendBlock(chainIndexer);
            ChainedHeader b = this.AppendBlock(chainIndexer);
            Assert.Equal(14, chainIndexer.Height);
            Assert.Equal(14, b.Height);
            Assert.Equal(b.HashBlock, chainIndexer.Tip.HashBlock);
        }

        /// <summary>
        /// Adapted from bitcoin core test, verify GetAncestor is using skip list in <see cref="ChainedHeader"/>.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/master/src/test/skiplist_tests.cpp"/>
        /// </summary>
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ChainedHeaderVerifySkipListForGetAncestor()
        {
            int skipListLength = 300000;

            // Want a chain of exact length so subtract the genesis block.
            ChainIndexer chainIndexer = this.CreateChain(skipListLength - 1);

            // Also want a copy in array form so can quickly verify indexing.
            var chainArray = new ChainedHeader[skipListLength];

            // Check skip height and build out array copy.
            foreach (ChainedHeader block in chainIndexer.EnumerateToTip(chainIndexer.Genesis))
            {
                if (block.Height > 0)
                    Assert.True(block.Skip.Height < block.Height);
                else
                    Assert.Null(block.Skip);
                chainArray[block.Height] = block;
            }

            // Do some random verification of GetAncestor().
            var random = new Random();
            int randCheckCount = 1000;
            for (int i = 0; i < randCheckCount; i++)
            {
                int from = random.Next(chainIndexer.Tip.Height - 1);
                int to = random.Next(from + 1);

                Assert.Equal(chainArray[chainIndexer.Tip.Height - 1].GetAncestor(from), chainArray[from]);
                Assert.Equal(chainArray[from].GetAncestor(to), chainArray[to]);
                Assert.Equal(chainArray[from].GetAncestor(0), chainArray[0]);
            }
        }

        /// <summary>
        /// Adapted from bitcoin core test, verify GetLocator is using skip list in <see cref="ChainedHeader"/>.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/master/src/test/skiplist_tests.cpp"/>
        /// </summary>
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ChainedHeaderVerifySkipListForGetLocator()
        {
            int mainLength = 100000;
            int branchLength = 50000;

            // Make a main chain 100000 blocks long.
            ChainIndexer chainIndexer = this.CreateChain(mainLength - 1);

            // Make a branch that splits off at block 49999, 50000 blocks long.
            ChainedHeader mainTip = chainIndexer.Tip;
            ChainedHeader block = mainTip.GetAncestor(branchLength - 1);
            for (int i = 0; i < branchLength; i++)
            {
                Block newBlock = TestUtils.CreateFakeBlock(this.network);
                newBlock.Header.HashPrevBlock = block.Header.GetHash();
                block = new ChainedHeader(newBlock.Header, newBlock.Header.GetHash(), block);
            }

            ChainedHeader branchTip = block;

            // Test 100 random starting points for locators.
            var rand = new Random();
            for (int n = 0; n < 100; n++)
            {
                // Find a random location along chain for locator < mainLength is on main chain > mainLength is on branch.
                int r = rand.Next(mainLength + branchLength);

                // Block to get locator for.
                ChainedHeader tip = r < mainLength ? mainTip.GetAncestor(r) : branchTip.GetAncestor(r - mainLength);

                // Get a block locator.
                BlockLocator locator = tip.GetLocator();

                // The first result must be the block itself, the last one must be genesis.
                Assert.Equal(tip.HashBlock, locator.Blocks.First());
                Assert.Equal(chainIndexer.Genesis.HashBlock, locator.Blocks.Last());

                // Entries 1 through 11 (inclusive) go back one step each.
                for (int i = 1; (i < 12) && (i < (locator.Blocks.Count - 1)); i++)
                {
                    ChainedHeader expectedBlock = tip.GetAncestor(tip.Height - i);
                    Assert.Equal(expectedBlock.HashBlock, locator.Blocks[i]);
                }

                // The further ones (excluding the last one) go back with exponential steps.
                int dist = 2;
                int height = tip.Height - 11 - dist;
                for (int i = 12; i < locator.Blocks.Count() - 1; i++)
                {
                    ChainedHeader expectedBlock = tip.GetAncestor(height);
                    Assert.Equal(expectedBlock.HashBlock, locator.Blocks[i]);
                    dist *= 2;
                    height -= dist;
                }
            }
        }

        private ChainIndexer CreateChain(int height)
        {
            return this.CreateChain(TestUtils.CreateFakeBlock(this.network).Header, height);
        }

        private ChainIndexer CreateChain(BlockHeader genesis, int height)
        {
            var chain = new ChainIndexer(this.network);

            var chainedHeaderPrev = chain.Tip;

            for (int i = 0; i < height; i++)
            {
                Block b = TestUtils.CreateFakeBlock(this.network);
                b.Header.HashPrevBlock = chainedHeaderPrev.HashBlock;

                chainedHeaderPrev = new ChainedHeader(b.Header, b.Header.GetHash(), chainedHeaderPrev);
            }

            chain.SetTip(chainedHeaderPrev);

            return chain;
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ChainIndexer chain in chainsIndexer)
            {
                Block block = TestUtils.CreateFakeBlock(this.network);
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chainsIndexer);
        }

        /// <summary>
        /// Returns the first common chained block header between two chains.
        /// </summary>
        /// <param name="chainSrc">The source chain.</param>
        /// <param name="otherChain">The other chain.</param>
        /// <returns>First common chained block header or <c>null</c>.</returns>
        private ChainedHeader FindFork(ChainIndexer chainSrc, ChainIndexer otherChain)
        {
            if (otherChain == null)
                throw new ArgumentNullException("otherChain");

            return chainSrc.FindFork(otherChain.Tip.EnumerateToGenesis().Select(o => o.HashBlock));
        }
    }
}