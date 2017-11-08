using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests
{
    public class TestBase
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for inherited tests.
        /// </summary>
        public TestBase()
        {
            this.loggerFactory = new LoggerFactory();
            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize();
        }

        public static DataFolder AssureEmptyDirAsDataFolder(string dir)
        {
            var dataFolder = new DataFolder(new NodeSettings { DataDir = AssureEmptyDir(dir) });
            return dataFolder;
        }

        public static string AssureEmptyDir(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);

            return dir;
        }

        public void AppendBlocksToChain(ConcurrentChain chain, IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (chain.Tip != null)
                    block.Header.HashPrevBlock = chain.Tip.HashBlock;
                chain.SetTip(block.Header);
            }
        }

        public List<Block> CreateBlocks(int amount, bool bigBlocks = false)
        {
            var blocks = new List<Block>();
            for (int i = 0; i < amount; i++)
            {
                Block block = this.CreateBlock(i);
                block.Header.HashPrevBlock = blocks.LastOrDefault()?.GetHash() ?? Network.Main.GenesisHash;
                blocks.Add(block);
            }

            return blocks;
        }

        public Block CreateBlock(int blockNumber, bool bigBlocks = false)
        {
            var block = new Block();

            int transactionCount = bigBlocks ? 1000 : 10;

            for (int j = 0; j < transactionCount; j++)
            {
                var trx = new Transaction();

                block.AddTransaction(new Transaction());

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                trx.AddInput(new TxIn(Script.Empty));
                trx.AddOutput(Money.COIN + j + blockNumber + 1, new Script(Enumerable.Range(1, 5).SelectMany(index => Guid.NewGuid().ToByteArray())));

                block.AddTransaction(trx);
            }

            block.UpdateMerkleRoot();

            return block;
        }
    }
}