using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Common
{
    public class TestBase
    {
        public Network Network { get; }

        /// <summary>
        /// Initializes logger factory for inherited tests.
        /// </summary>
        public TestBase(Network network)
        {
            this.Network = network;
            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize(this.Network); 
        }
        
        public static string AssureEmptyDir(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);

            return dir;
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
            var dataFolder = new DataFolder(new NodeSettings(args: new string[] { $"-datadir={AssureEmptyDir(directoryPath)}" }).DataDir);
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
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            return Path.Combine("TestCase", caller.GetType().Name, callingMethod);
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
                block.Header.HashPrevBlock = blocks.LastOrDefault()?.GetHash() ?? this.Network.GenesisHash;
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
