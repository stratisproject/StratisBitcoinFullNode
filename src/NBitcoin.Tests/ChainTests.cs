﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace NBitcoin.Tests
{
    public class ChainTests
	{
        public ChainTests()
        {
            // These flags may get set due to static network initializers
            // which include the initializers for Stratis.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

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
			for(int i = 0; i < 600; i++)
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
			Assert.Equal(cchain.GetBlock(b5.HashBlock), null);
			Assert.Equal(cchain.GetBlock(b2.HashBlock), null);

			Assert.Equal(cchain.SetTip(b5), b1);
			Assert.Equal(cchain.GetBlock(b5.HashBlock), chain.Tip);

			chain.SetTip(b2);
			AddBlock(chain);
			AddBlock(chain);
			var b5b = AddBlock(chain);
			var b6b = AddBlock(chain);

			Assert.Equal(cchain.SetTip(b6b), b2);

			Assert.Equal(cchain.GetBlock(b5.HashBlock), null);
			Assert.Equal(cchain.GetBlock(b2.HashBlock), b2);
			Assert.Equal(cchain.GetBlock(6), b6b);
			Assert.Equal(cchain.GetBlock(5), b5b);
		}

		private ChainedBlock AddBlock(ConcurrentChain chain)
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
			foreach(var b in chain.EnumerateAfter(chain.Genesis))
			{
				chain.GetBlock(0);
			}

			foreach(var b in chain.ToEnumerable(false))
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

		private void AssertFork(ConcurrentChain chain, ConcurrentChain chain2, ChainedBlock expectedFork)
		{
			var fork = chain.FindFork(chain2);
			Assert.Equal(expectedFork, fork);
			fork = chain.Tip.FindFork(chain2.Tip);
			Assert.Equal(expectedFork, fork);

			var temp = chain;
			chain = chain2;
			chain2 = temp;

			fork = chain.FindFork(chain2);
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

			foreach(var history in histories)
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
			foreach(var h in main.ToEnumerable(false))
			{
				Assert.True(h.Validate(Network.Main));
			}
		}
		
		private byte[] LoadMainChain()
		{
			if(!File.Exists("MainChain1.dat"))
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

		private ConcurrentChain CreateChain(int height)
		{
			return CreateChain(TestUtils.CreateFakeBlock().Header, height);
		}

		private ConcurrentChain CreateChain(BlockHeader genesis, int height)
		{
			var chain = new ConcurrentChain(genesis);
			for(int i = 0; i < height; i++)
			{
				var b = TestUtils.CreateFakeBlock();
				b.Header.HashPrevBlock = chain.Tip.HashBlock;
				chain.SetTip(b.Header);
			}
			return chain;
		}


		public ChainedBlock AppendBlock(ChainedBlock previous, params ConcurrentChain[] chains)
		{
			ChainedBlock last = null;
			var nonce = RandomUtils.GetUInt32();
			foreach(var chain in chains)
			{
				var block = TestUtils.CreateFakeBlock(new Transaction());
				block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
				block.Header.Nonce = nonce;
				if(!chain.TrySetTip(block.Header, out last))
					throw new InvalidOperationException("Previous not existing");
			}
			return last;
		}
		private ChainedBlock AppendBlock(params ConcurrentChain[] chains)
		{
			ChainedBlock index = null;
			return AppendBlock(index, chains);
		}

        /// <summary> 
        /// Adapted from bitcoin core test, verify skip list creation in <see cref="ChainedBlock"/>.
        /// <seealso cref="https://github.com/bitcoin/bitcoin/blob/master/src/test/skiplist_tests.cpp"/>
        /// </summary>
        [Fact]
        [Trait("UnitTest", "UnitTest")]        
        public void ChainedBlockVerifySkipListBuildsProperly()
        {
            const int skipListLength = 300000;

            // Want a chain of exact length so subtract the genesis block.
            ConcurrentChain chain = this.CreateChain(skipListLength - 1);

            // Also want a copy in array form so can quickly verify indexing.
            ChainedBlock[] chainArray = new ChainedBlock[skipListLength];

            // Check skip height and build out array copy.
            foreach (ChainedBlock block in chain.EnumerateToTip(chain.Genesis))
            {
                if (block.Height > 0)
                    Assert.True(block.Skip.Height < block.Height);
                else
                    Assert.Null(block.Skip);
                chainArray[block.Height] = block;
            }

            // Do some random verification of GetAncestor().
            Random random = new Random();
            const int randCheckCount = 1000;
            for (int i = 0; i < randCheckCount; i++)
            {
                int from = random.Next(chain.Tip.Height - 1);
                int to = random.Next(from + 1);

                Assert.Equal(chainArray[chain.Tip.Height - 1].GetAncestor(from), chainArray[from]);
                Assert.Equal(chainArray[from].GetAncestor(to), chainArray[to]);
                Assert.Equal(chainArray[from].GetAncestor(0), chainArray[0]);
            }
        }

        // TODO: Implement this test
        // https://github.com/bitcoin/bitcoin/blob/master/src/test/skiplist_tests.cpp
        //BOOST_AUTO_TEST_CASE(getlocator_test)
        //{
        //    // Build a main chain 100000 blocks long.
        //    std::vector<uint256> vHashMain(100000);
        //    std::vector<CBlockIndex> vBlocksMain(100000);
        //    for (unsigned int i = 0; i < vBlocksMain.size(); i++)
        //    {
        //        vHashMain[i] = ArithToUint256(i); // Set the hash equal to the height, so we can quickly check the distances.
        //        vBlocksMain[i].nHeight = i;
        //        vBlocksMain[i].pprev = i ? &vBlocksMain[i - 1] : nullptr;
        //        vBlocksMain[i].phashBlock = &vHashMain[i];
        //        vBlocksMain[i].BuildSkip();
        //        BOOST_CHECK_EQUAL((int)UintToArith256(vBlocksMain[i].GetBlockHash()).GetLow64(), vBlocksMain[i].nHeight);
        //        BOOST_CHECK(vBlocksMain[i].pprev == nullptr || vBlocksMain[i].nHeight == vBlocksMain[i].pprev->nHeight + 1);
        //    }

        //    // Build a branch that splits off at block 49999, 50000 blocks long.
        //    std::vector<uint256> vHashSide(50000);
        //    std::vector<CBlockIndex> vBlocksSide(50000);
        //    for (unsigned int i = 0; i < vBlocksSide.size(); i++)
        //    {
        //        vHashSide[i] = ArithToUint256(i + 50000 + (arith_uint256(1) << 128)); // Add 1<<128 to the hashes, so GetLow64() still returns the height.
        //        vBlocksSide[i].nHeight = i + 50000;
        //        vBlocksSide[i].pprev = i ? &vBlocksSide[i - 1] : (vBlocksMain.data() + 49999);
        //        vBlocksSide[i].phashBlock = &vHashSide[i];
        //        vBlocksSide[i].BuildSkip();
        //        BOOST_CHECK_EQUAL((int)UintToArith256(vBlocksSide[i].GetBlockHash()).GetLow64(), vBlocksSide[i].nHeight);
        //        BOOST_CHECK(vBlocksSide[i].pprev == nullptr || vBlocksSide[i].nHeight == vBlocksSide[i].pprev->nHeight + 1);
        //    }

        //    // Build a CChain for the main branch.
        //    CChain chain;
        //    chain.SetTip(&vBlocksMain.back());

        //    // Test 100 random starting points for locators.
        //    for (int n = 0; n < 100; n++)
        //    {
        //        int r = InsecureRandRange(150000);
        //        CBlockIndex* tip = (r < 100000) ? &vBlocksMain[r] : &vBlocksSide[r - 100000];
        //        CBlockLocator locator = chain.GetLocator(tip);

        //        // The first result must be the block itself, the last one must be genesis.
        //        BOOST_CHECK(locator.vHave.front() == tip->GetBlockHash());
        //        BOOST_CHECK(locator.vHave.back() == vBlocksMain[0].GetBlockHash());

        //        // Entries 1 through 11 (inclusive) go back one step each.
        //        for (unsigned int i = 1; i < 12 && i < locator.vHave.size() - 1; i++)
        //        {
        //            BOOST_CHECK_EQUAL(UintToArith256(locator.vHave[i]).GetLow64(), tip->nHeight - i);
        //        }

        //        // The further ones (excluding the last one) go back with exponential steps.
        //        unsigned int dist = 2;
        //        for (unsigned int i = 12; i < locator.vHave.size() - 1; i++)
        //        {
        //            BOOST_CHECK_EQUAL(UintToArith256(locator.vHave[i - 1]).GetLow64() - UintToArith256(locator.vHave[i]).GetLow64(), dist);
        //            dist *= 2;
        //        }
        //    }
        //}
    }
}
