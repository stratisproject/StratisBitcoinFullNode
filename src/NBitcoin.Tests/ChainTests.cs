using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace NBitcoin.Tests
{
    public class ChainTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCloneConcurrentChain()
        {
            var chain = new ConcurrentChain(Network.Main);
            var common = AppendBlock(chain);
            var fork = AppendBlock(chain);
            var fork2 = AppendBlock(chain);

            Assert.True(chain.Tip == fork2);
            var clone = chain.Clone();
            Assert.True(clone.Tip == fork2);
        }


        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanSaveChain()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            AppendBlock(chain);
            var fork = AppendBlock(chain);
            AppendBlock(chain);



            var chain2 = new ConcurrentChain(chain.ToBytes());
            Assert.True(chain.SameTip(chain2));
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
                var bytes = RandomUtils.GetBytes(120);
                new Script(bytes).ToString();
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanLoadAndSaveConcurrentChain()
        {
            ConcurrentChain cchain = new ConcurrentChain();
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AddBlock(chain);
            AddBlock(chain);
            AddBlock(chain);

            cchain.SetTip(chain);

            var bytes = cchain.ToBytes();
            cchain = new ConcurrentChain();
            cchain.Load(bytes);

            Assert.Equal(cchain.Tip, chain.Tip);
            Assert.NotNull(cchain.GetBlock(0));

            cchain = new ConcurrentChain(Network.TestNet);
            cchain.Load(cchain.ToBytes());
            Assert.NotNull(cchain.GetBlock(0));
        }
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildConcurrentChain()
        {
            ConcurrentChain cchain = new ConcurrentChain();
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            Assert.Null(cchain.SetTip(chain.Tip));
            var b0 = cchain.Tip;
            Assert.Equal(cchain.Tip, chain.Tip);

            var b1 = AddBlock(chain);
            var b2 = AddBlock(chain);
            AddBlock(chain);
            AddBlock(chain);
            var b5 = AddBlock(chain);

            Assert.Equal(cchain.SetTip(chain.Tip), b0);
            Assert.Equal(cchain.Tip, chain.Tip);

            Assert.Equal(cchain.GetBlock(5), chain.Tip);
            Assert.Equal(cchain.GetBlock(b5.HashBlock), chain.Tip);

            Assert.Equal(cchain.SetTip(b1), b1);
            Assert.Null(cchain.GetBlock(b5.HashBlock));
            Assert.Null(cchain.GetBlock(b2.HashBlock));

            Assert.Equal(cchain.SetTip(b5), b1);
            Assert.Equal(cchain.GetBlock(b5.HashBlock), chain.Tip);

            chain.SetTip(b2);
            AddBlock(chain);
            AddBlock(chain);
            var b5b = AddBlock(chain);
            var b6b = AddBlock(chain);

            Assert.Equal(cchain.SetTip(b6b), b2);

            Assert.Null(cchain.GetBlock(b5.HashBlock));
            Assert.Equal(cchain.GetBlock(b2.HashBlock), b2);
            Assert.Equal(cchain.GetBlock(6), b6b);
            Assert.Equal(cchain.GetBlock(5), b5b);
        }

        private ChainedHeader AddBlock(ConcurrentChain chain)
        {
            BlockHeader header = new BlockHeader();
            header.Nonce = RandomUtils.GetUInt32();
            header.HashPrevBlock = chain.Tip.HashBlock;
            chain.SetTip(header);
            return chain.GetBlock(header.GetHash());
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanIterateConcurrentChain()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            AppendBlock(chain);
            AppendBlock(chain);
            foreach (var b in chain.EnumerateAfter(chain.Genesis))
            {
                chain.GetBlock(0);
            }

            foreach (var b in chain.ToEnumerable(false))
            {
                chain.GetBlock(0);
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildChain()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            AppendBlock(chain);
            AppendBlock(chain);
            var b = AppendBlock(chain);
            Assert.Equal(4, chain.Height);
            Assert.Equal(4, b.Height);
            Assert.Equal(b.HashBlock, chain.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanFindFork()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            ConcurrentChain chain2 = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            var fork = AppendBlock(chain);
            var tip = AppendBlock(chain);

            AssertFork(chain, chain2, chain.Genesis);
            chain2 = new ConcurrentChain(Network.TestNet);
            AssertFork(chain, chain2, null);
            chain2 = new ConcurrentChain(Network.Main);
            chain2.SetTip(fork);
            AssertFork(chain, chain2, fork);
            chain2.SetTip(tip);
            AssertFork(chain, chain2, tip);
        }

        private void AssertFork(ConcurrentChain chain, ConcurrentChain chain2, ChainedHeader expectedFork)
        {
            var fork = this.FindFork(chain, chain2);
            Assert.Equal(expectedFork, fork);
            fork = chain.Tip.FindFork(chain2.Tip);
            Assert.Equal(expectedFork, fork);

            var temp = chain;
            chain = chain2;
            chain2 = temp;

            fork = this.FindFork(chain, chain2);
            Assert.Equal(expectedFork, fork);
            fork = chain.Tip.FindFork(chain2.Tip);
            Assert.Equal(expectedFork, fork);
        }

#if !NOFILEIO
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCalculateDifficulty()
        {
            var main = new ConcurrentChain(LoadMainChain());
            var histories = File.ReadAllText("data/targethistory.csv").Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var history in histories)
            {
                var height = int.Parse(history.Split(',')[0]);
                var expectedTarget = new Target(new BouncyCastle.Math.BigInteger(history.Split(',')[1], 10));

                var block = main.GetBlock(height).Header;

                Assert.Equal(expectedTarget, block.Bits);
                var target = main.GetWorkRequired(Network.Main, height);
                Assert.Equal(expectedTarget, target);
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanValidateChain()
        {
            var main = new ConcurrentChain(LoadMainChain());
            foreach (var h in main.ToEnumerable(false))
            {
                Assert.True(h.Validate(Network.Main));
            }
        }

        private byte[] LoadMainChain()
        {
            if (!File.Exists("MainChain1.dat"))
            {
                HttpClient client = new HttpClient();
                var bytes = client.GetByteArrayAsync("https://aois.blob.core.windows.net/public/MainChain1.dat").GetAwaiter().GetResult();
                File.WriteAllBytes("MainChain1.dat", bytes);
            }
            return File.ReadAllBytes("MainChain1.dat");
        }
#endif

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanEnumerateAfterChainedBlock()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            var a = AppendBlock(chain);
            var b = AppendBlock(chain);
            var c = AppendBlock(chain);

            Assert.True(chain.EnumerateAfter(a).SequenceEqual(new[] { b, c }));

            var d = AppendBlock(chain);

            var enumerator = chain.EnumerateAfter(b).GetEnumerator();
            enumerator.MoveNext();
            Assert.True(enumerator.Current == c);

            chain.SetTip(b);
            var cc = AppendBlock(chain);
            var dd = AppendBlock(chain);

            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanBuildChain2()
        {
            ConcurrentChain chain = CreateChain(10);
            AppendBlock(chain);
            AppendBlock(chain);
            AppendBlock(chain);
            var b = AppendBlock(chain);
            Assert.Equal(14, chain.Height);
            Assert.Equal(14, b.Height);
            Assert.Equal(b.HashBlock, chain.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanForkBackward()
        {
            ConcurrentChain chain = new ConcurrentChain(Network.Main);
            AppendBlock(chain);
            AppendBlock(chain);
            var fork = AppendBlock(chain);

            //Test single block back fork
            var last = AppendBlock(chain);
            Assert.Equal(4, chain.Height);
            Assert.Equal(4, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);
            Assert.Equal(fork.HashBlock, chain.SetTip(fork).HashBlock);
            Assert.Equal(3, chain.Height);
            Assert.Equal(3, fork.Height);
            Assert.Equal(fork.HashBlock, chain.Tip.HashBlock);
            Assert.Null(chain.GetBlock(last.HashBlock));
            Assert.NotNull(chain.GetBlock(fork.HashBlock));

            //Test 3 blocks back fork
            var b1 = AppendBlock(chain);
            var b2 = AppendBlock(chain);
            last = AppendBlock(chain);
            Assert.Equal(6, chain.Height);
            Assert.Equal(6, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);

            Assert.Equal(fork.HashBlock, chain.SetTip(fork).HashBlock);
            Assert.Equal(3, chain.Height);
            Assert.Equal(3, fork.Height);
            Assert.Equal(fork.HashBlock, chain.Tip.HashBlock);
            Assert.Null(chain.GetBlock(last.HashBlock));
            Assert.Null(chain.GetBlock(b1.HashBlock));
            Assert.Null(chain.GetBlock(b2.HashBlock));

            chain.SetTip(last);
            Assert.Equal(6, chain.Height);
            Assert.Equal(6, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanForkBackwardPartialChain()
        {
            ConcurrentChain chain = CreateChain(10);
            AppendBlock(chain);
            AppendBlock(chain);
            var fork = AppendBlock(chain);

            //Test single block back fork
            var last = AppendBlock(chain);
            Assert.Equal(14, chain.Height);
            Assert.Equal(14, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);
            Assert.Equal(fork.HashBlock, chain.SetTip(fork).HashBlock);
            Assert.Equal(13, chain.Height);
            Assert.Equal(13, fork.Height);
            Assert.Equal(fork.HashBlock, chain.Tip.HashBlock);
            Assert.Null(chain.GetBlock(last.HashBlock));
            Assert.NotNull(chain.GetBlock(fork.HashBlock));

            //Test 3 blocks back fork
            var b1 = AppendBlock(chain);
            var b2 = AppendBlock(chain);
            last = AppendBlock(chain);
            Assert.Equal(16, chain.Height);
            Assert.Equal(16, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);

            Assert.Equal(fork.HashBlock, chain.SetTip(fork).HashBlock);
            Assert.Equal(13, chain.Height);
            Assert.Equal(13, fork.Height);
            Assert.Equal(fork.HashBlock, chain.Tip.HashBlock);
            Assert.Null(chain.GetBlock(last.HashBlock));
            Assert.Null(chain.GetBlock(b1.HashBlock));
            Assert.Null(chain.GetBlock(b2.HashBlock));

            chain.SetTip(last);
            Assert.Equal(16, chain.Height);
            Assert.Equal(16, last.Height);
            Assert.Equal(last.HashBlock, chain.Tip.HashBlock);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanForkSide()
        {
            ConcurrentChain side = new ConcurrentChain(Network.Main);
            ConcurrentChain main = new ConcurrentChain(Network.Main);
            AppendBlock(side, main);
            AppendBlock(side, main);
            var common = AppendBlock(side, main);
            var sideb = AppendBlock(side);
            var mainb1 = AppendBlock(main);
            var mainb2 = AppendBlock(main);
            var mainb3 = AppendBlock(main);
            Assert.Equal(common.HashBlock, side.SetTip(main.Tip).HashBlock);
            Assert.NotNull(side.GetBlock(mainb1.HashBlock));
            Assert.NotNull(side.GetBlock(mainb2.HashBlock));
            Assert.NotNull(side.GetBlock(mainb3.HashBlock));
            Assert.NotNull(side.GetBlock(common.HashBlock));
            Assert.Null(side.GetBlock(sideb.HashBlock));

            Assert.Equal(common.HashBlock, side.SetTip(sideb).HashBlock);
            Assert.Null(side.GetBlock(mainb1.HashBlock));
            Assert.Null(side.GetBlock(mainb2.HashBlock));
            Assert.Null(side.GetBlock(mainb3.HashBlock));
            Assert.NotNull(side.GetBlock(sideb.HashBlock));
        }
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanForkSidePartialChain()
        {
            var genesis = TestUtils.CreateFakeBlock();
            ConcurrentChain side = new ConcurrentChain(genesis.Header);
            ConcurrentChain main = new ConcurrentChain(genesis.Header);
            AppendBlock(side, main);
            AppendBlock(side, main);
            var common = AppendBlock(side, main);
            var sideb = AppendBlock(side);
            var mainb1 = AppendBlock(main);
            var mainb2 = AppendBlock(main);
            var mainb3 = AppendBlock(main);
            Assert.Equal(common.HashBlock, side.SetTip(main.Tip).HashBlock);
            Assert.NotNull(side.GetBlock(mainb1.HashBlock));
            Assert.NotNull(side.GetBlock(mainb2.HashBlock));
            Assert.NotNull(side.GetBlock(mainb3.HashBlock));
            Assert.NotNull(side.GetBlock(common.HashBlock));
            Assert.Null(side.GetBlock(sideb.HashBlock));

            Assert.Equal(common.HashBlock, side.SetTip(sideb).HashBlock);
            Assert.Null(side.GetBlock(mainb1.HashBlock));
            Assert.Null(side.GetBlock(mainb2.HashBlock));
            Assert.Null(side.GetBlock(mainb3.HashBlock));
            Assert.NotNull(side.GetBlock(sideb.HashBlock));
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
            ConcurrentChain chain = this.CreateChain(skipListLength - 1);

            // Also want a copy in array form so can quickly verify indexing.
            ChainedHeader[] chainArray = new ChainedHeader[skipListLength];

            // Check skip height and build out array copy.
            foreach (ChainedHeader block in chain.EnumerateToTip(chain.Genesis))
            {
                if (block.Height > 0)
                    Assert.True(block.Skip.Height < block.Height);
                else
                    Assert.Null(block.Skip);
                chainArray[block.Height] = block;
            }

            // Do some random verification of GetAncestor().
            Random random = new Random();
            int randCheckCount = 1000;
            for (int i = 0; i < randCheckCount; i++)
            {
                int from = random.Next(chain.Tip.Height - 1);
                int to = random.Next(from + 1);

                Assert.Equal(chainArray[chain.Tip.Height - 1].GetAncestor(from), chainArray[from]);
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
            ConcurrentChain chain = this.CreateChain(mainLength - 1);

            // Make a branch that splits off at block 49999, 50000 blocks long.
            ChainedHeader mainTip = chain.Tip;
            ChainedHeader block = mainTip.GetAncestor(branchLength - 1);
            for (int i = 0; i < branchLength; i++)
            {
                Block newBlock = TestUtils.CreateFakeBlock();
                newBlock.Header.HashPrevBlock = block.Header.GetHash();
                block = new ChainedHeader(newBlock.Header, newBlock.Header.GetHash(), block);
            }
            ChainedHeader branchTip = block;

            // Test 100 random starting points for locators.
            Random rand = new Random();
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
                Assert.Equal(chain.Genesis.HashBlock, locator.Blocks.Last());

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

        private ConcurrentChain CreateChain(int height)
        {
            return CreateChain(TestUtils.CreateFakeBlock().Header, height);
        }

        private ConcurrentChain CreateChain(BlockHeader genesis, int height)
        {
            var chain = new ConcurrentChain(genesis);
            for (int i = 0; i < height; i++)
            {
                var b = TestUtils.CreateFakeBlock();
                b.Header.HashPrevBlock = chain.Tip.HashBlock;
                chain.SetTip(b.Header);
            }
            return chain;
        }


        public ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            var nonce = RandomUtils.GetUInt32();
            foreach (var chain in chains)
            {
                var block = TestUtils.CreateFakeBlock(new Transaction());
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return AppendBlock(index, chains);
        }

        /// <summary>
        /// Returns the first common chained block header between two chains.
        /// </summary>
        /// <param name="chainSrc">The source chain.</param>
        /// <param name="otherChain">The other chain.</param>
        /// <returns>First common chained block header or <c>null</c>.</returns>
        private ChainedHeader FindFork(ChainBase chainSrc, ChainBase otherChain)
        {
            if (otherChain == null)
                throw new ArgumentNullException("otherChain");

            return chainSrc.FindFork(otherChain.Tip.EnumerateToGenesis().Select(o => o.HashBlock));
        }
    }
}
