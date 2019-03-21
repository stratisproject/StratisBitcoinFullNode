using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class ConsensusChainIndexerExtentions
    {
        /// <summary>
        /// Sets the tip of this chain based upon another block header.
        /// </summary>
        /// <param name="header">The block header to set to tip.</param>
        /// <returns>Whether the tip was set successfully.</returns>
        public static bool SetTip(this ConsensusChainIndexer chainIndexer, BlockHeader header)
        {
            ChainedHeader chainedHeader;
            return chainIndexer.TrySetTip(header, out chainedHeader);
        }

        public static bool TrySetTip(this ConsensusChainIndexer chainIndexer, BlockHeader header, out ChainedHeader chainedHeader)
        {
            if (header == null)
                throw new ArgumentNullException("header");

            chainedHeader = null;
            ChainedHeader prev = chainIndexer.GetBlock(header.HashPrevBlock);
            if (prev == null)
                return false;

            chainedHeader = new ChainedHeader(header, header.GetHash(), chainIndexer.GetBlock(header.HashPrevBlock));
            chainIndexer.SetTip(chainedHeader);
            return true;
        }

        public static ChainedHeader SetTip(this ConsensusChainIndexer chainIndexer, ChainedHeader block)
        {
            int height = chainIndexer.Tip == null ? -1 : chainIndexer.Tip.Height;
            foreach (ChainedHeader orphaned in chainIndexer.EnumerateThisToFork(block))
            {
                chainIndexer.Remove(orphaned);
                height--;
            }

            ChainedHeader fork = chainIndexer.GetBlock(height);
            foreach (ChainedHeader newBlock in block.EnumerateToGenesis().TakeWhile(c => c != fork))
            {
                chainIndexer.Add(newBlock);
            }

            return fork;
        }

        private static IEnumerable<ChainedHeader> EnumerateThisToFork(this ConsensusChainIndexer chainIndexer, ChainedHeader block)
        {
            if (chainIndexer.Tip == null)
                yield break;

            ChainedHeader tip = chainIndexer.Tip;
            while (true)
            {
                if (ReferenceEquals(null, block) || ReferenceEquals(null, tip))
                    throw new InvalidOperationException("No fork found between the two chains");

                if (tip.Height > block.Height)
                {
                    yield return tip;
                    tip = tip.Previous;
                }
                else if (tip.Height < block.Height)
                {
                    block = block.Previous;
                }
                else if (tip.Height == block.Height)
                {
                    if (tip.HashBlock == block.HashBlock)
                        break;

                    yield return tip;

                    block = block.Previous;
                    tip = tip.Previous;
                }
            }
        }

        public static void Load(this ConsensusChainIndexer chainIndexer, byte[] chain)
        {
            using (var ms = new MemoryStream(chain))
            {
                chainIndexer.Load(ms);
            }
        }

        public static void Load(this ConsensusChainIndexer chainIndexer, Stream stream)
        {
            chainIndexer.Load(new BitcoinStream(stream, false));
        }

        public static void Load(this ConsensusChainIndexer chainIndexer, BitcoinStream stream)
        {
            stream.ConsensusFactory = chainIndexer.Network.Consensus.ConsensusFactory;

            try
            {
                int height = 0;
                while (true)
                {
                    uint256.MutableUint256 id = null;
                    stream.ReadWrite<uint256.MutableUint256>(ref id);
                    BlockHeader header = null;
                    stream.ReadWrite(ref header);
                    if (height == 0)
                    {
                        chainIndexer.Add(new ChainedHeader(header, header.GetHash(), 0));
                    }
                    else if (chainIndexer.Tip.HashBlock == header.HashPrevBlock && !(header.IsNull && header.Nonce == 0))
                    {
                        chainIndexer.Add(new ChainedHeader(header, id.Value, chainIndexer.Tip));
                    }
                    else
                        break;

                    height++;
                }
            }
            catch (EndOfStreamException)
            {
            }
        }

        public static byte[] ToBytes(this ConsensusChainIndexer chainIndexer)
        {
            using (var ms = new MemoryStream())
            {
                chainIndexer.WriteTo(ms);
                return ms.ToArray();
            }
        }

        public static void WriteTo(this ConsensusChainIndexer chainIndexer, Stream stream)
        {
            chainIndexer.WriteTo(new BitcoinStream(stream, true));
        }

        public static void WriteTo(this ConsensusChainIndexer chainIndexer, BitcoinStream stream)
        {
            stream.ConsensusFactory = chainIndexer.Network.Consensus.ConsensusFactory;

            for (int i = 0; i < chainIndexer.Tip.Height + 1; i++)
            {
                ChainedHeader block = chainIndexer.GetBlock(i);
                stream.ReadWrite(block.HashBlock.AsBitcoinSerializable());
                stream.ReadWrite(block.Header);
            }
        }
    }

    public class TestBase
    {
        public Network Network { get; protected set; }
        public DBreezeSerializer DBreezeSerializer { get; }

        /// <summary>
        /// Initializes logger factory for inherited tests.
        /// </summary>
        public TestBase(Network network)
        {
            this.Network = network;
            this.DBreezeSerializer = new DBreezeSerializer(network);
        }

        public static string AssureEmptyDir(string dir)
        {
            string uniqueDirName = $"{dir}-{DateTime.UtcNow:ddMMyyyyTHH.mm.ss.fff}";
            Directory.CreateDirectory(uniqueDirName);
            return uniqueDirName;
        }

        /// <summary>
        /// Creates a directory and initializes a <see cref="DataFolder"/> for a test, based on the name of the class containing the test and the name of the test.
        /// </summary>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The <see cref="DataFolder"/> that was initialized.</returns>
        public static DataFolder CreateDataFolder(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string directoryPath = GetTestDirectoryPath(caller, callingMethod);
            var dataFolder = new DataFolder(new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new string[] { $"-datadir={AssureEmptyDir(directoryPath)}" }).DataDir);
            return dataFolder;
        }

        /// <summary>
        /// Creates a directory for a test, based on the name of the class containing the test and the name of the test.
        /// </summary>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory that was created.</returns>
        public static string CreateTestDir(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string directoryPath = GetTestDirectoryPath(caller, callingMethod);
            return AssureEmptyDir(directoryPath);
        }

        /// <summary>
        /// Creates a directory for a test.
        /// </summary>
        /// <param name="testDirectory">The directory in which the test files are contained.</param>
        /// <returns>The path of the directory that was created.</returns>
        public static string CreateTestDir(string testDirectory)
        {
            string directoryPath = GetTestDirectoryPath(testDirectory);
            return AssureEmptyDir(directoryPath);
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            return GetTestDirectoryPath(Path.Combine(caller.GetType().Name, callingMethod));
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="testDirectory">The directory in which the test files are contained.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(string testDirectory)
        {
            return Path.Combine("..", "..", "..", "..", "TestCase", testDirectory);
        }

        public void AppendBlocksToChain(ConsensusChainIndexer chainIndexer, IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                if (chainIndexer.Tip != null)
                    block.Header.HashPrevBlock = chainIndexer.Tip.HashBlock;
                chainIndexer.SetTip(block.Header);
            }
        }

        public List<Block> CreateBlocks(int amount, bool bigBlocks = false)
        {
            var blocks = new List<Block>();
            for (int i = 0; i < amount; i++)
            {
                Block block = this.CreateBlock(i);
                block.Header.HashPrevBlock = blocks.LastOrDefault()?.GetHash() ?? this.Network.GenesisHash;
                blocks.Add(block);
            }

            return blocks;
        }

        private Block CreateBlock(int blockNumber, bool bigBlocks = false)
        {
            Block block = this.Network.CreateBlock();

            int transactionCount = bigBlocks ? 1000 : 10;

            for (int j = 0; j < transactionCount; j++)
            {
                Transaction trx = this.Network.CreateTransaction();

                // Coinbase
                block.AddTransaction(this.Network.CreateTransaction());

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber + 1, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                block.AddTransaction(trx);
            }

            block.UpdateMerkleRoot();

            return block;
        }

        public ProvenBlockHeader CreateNewProvenBlockHeaderMock(PosBlock posBlock = null)
        {
            PosBlock block = posBlock == null ? this.CreatePosBlock() : posBlock;
            ProvenBlockHeader provenBlockHeader = ((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

            return provenBlockHeader;
        }

        /// <summary>
        /// Creates a list of Proof of Stake blocks.
        /// </summary>
        /// <param name="amount">The amount of blocks to create.</param>
        public List<Block> CreatePosBlocks(int amount)
        {
            var blocks = new List<Block>();
            for (int i = 0; i < amount; i++)
            {
                PosBlock block = this.CreatePosBlock();
                block.Header.HashPrevBlock = blocks.LastOrDefault()?.GetHash() ?? this.Network.GenesisHash;
                blocks.Add(block);
            }

            return blocks;
        }

        public PosBlock CreatePosBlock()
        {
            // Create coinstake Tx.
            Transaction previousTx = this.Network.CreateTransaction();
            previousTx.AddOutput(new TxOut());

            Transaction coinstakeTx = this.Network.CreateTransaction();
            coinstakeTx.AddOutput(new TxOut(0, Script.Empty));
            coinstakeTx.AddOutput(new TxOut(50, new Script()));
            coinstakeTx.AddInput(previousTx, 0);
            coinstakeTx.IsCoinStake.Should().BeTrue();
            coinstakeTx.IsCoinBase.Should().BeFalse();

            // Create coinbase Tx.
            Transaction coinBaseTx = this.Network.CreateTransaction();
            coinBaseTx.AddOutput(100, new Script());
            coinBaseTx.AddInput(new TxIn());
            coinBaseTx.IsCoinBase.Should().BeTrue();
            coinBaseTx.IsCoinStake.Should().BeFalse();

            var block = (PosBlock)this.Network.CreateBlock();
            block.AddTransaction(coinBaseTx);
            block.AddTransaction(coinstakeTx);
            block.BlockSignature = new BlockSignature { Signature = new byte[] { 0x2, 0x3 } };

            return block;
        }

        /// <summary>
        /// Builds a chain of proven headers.
        /// </summary>
        /// <param name="blockCount">The amount of blocks to chain.</param>
        /// <param name="startingHeader">Build the chain from this header, if not start from genesis.</param>
        /// <returns>Tip of a created chain of headers.</returns>
        public ChainedHeader BuildProvenHeaderChain(int blockCount, ChainedHeader startingHeader = null)
        {
            startingHeader = startingHeader ?? ChainedHeadersHelper.CreateGenesisChainedHeader(this.Network);

            for (int i = 1; i < blockCount; i++)
            {
                PosBlock block = this.CreatePosBlock();
                ProvenBlockHeader header = ((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateProvenBlockHeader(block);

                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = startingHeader.HashBlock;
                header.Bits = Target.Difficulty1;

                ChainedHeader prevHeader = startingHeader;
                startingHeader = new ChainedHeader(header, header.GetHash(), prevHeader.Height + 1);

                startingHeader.SetPrivatePropertyValue("Previous", prevHeader);
                prevHeader.Next.Add(startingHeader);
            }

            return startingHeader;
        }

        public ChainedHeader BuildProvenHeaderChainFromBlocks(List<Block> posBlocks)
        {
            ChainedHeader currentHeader = ChainedHeadersHelper.CreateGenesisChainedHeader(this.Network);

            foreach (PosBlock posBlock in posBlocks)
            {
                ProvenBlockHeader header = ((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateProvenBlockHeader(posBlock);

                header.Nonce = RandomUtils.GetUInt32();
                header.HashPrevBlock = currentHeader.HashBlock;
                header.Bits = Target.Difficulty1;

                ChainedHeader prevHeader = currentHeader;
                currentHeader = new ChainedHeader(header, header.GetHash(), prevHeader);

                prevHeader.Next.Add(currentHeader);
            }

            return currentHeader;
        }

        public void CompareCollections(List<ChainedHeader> chainedHeaders, SortedDictionary<int, ProvenBlockHeader> sortedDictionary)
        {
            Assert.Equal(chainedHeaders.Count, sortedDictionary.Count);

            for (int i = 0; i < chainedHeaders.Count; i++)
            {
                Assert.Equal(chainedHeaders[i].HashBlock, sortedDictionary.ElementAt(i).Value.GetHash());
                if (i > 0)
                {
                    Assert.Equal(sortedDictionary.ElementAt(i - 1).Value.GetHash(), sortedDictionary.ElementAt(i).Value.HashPrevBlock);
                }
            }
        }

        public SortedDictionary<int, ProvenBlockHeader> ConvertToDictionaryOfProvenHeaders(ChainedHeader tip)
        {
            var headers = new SortedDictionary<int, ProvenBlockHeader>();

            ChainedHeader currentHeader = tip;

            while (currentHeader.Height != 0)
            {
                headers.Add(currentHeader.Height, currentHeader.Header as ProvenBlockHeader);

                currentHeader = currentHeader.Previous;
            }

            return headers;
        }
    }
}