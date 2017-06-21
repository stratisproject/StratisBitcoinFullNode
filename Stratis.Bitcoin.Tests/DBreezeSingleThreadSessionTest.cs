using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Consensus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{
    [TestClass]
    public class DBreezeSingleThreadSessionTest : TestBase
    {
        public DBreezeSingleThreadSessionTest()
        {
        }

        [TestMethod]
        public void NBitcoinSerializeWithBitcoinSerializableReturnsAsBytes()
        {
            Block block = new Block();

            var result = DBreezeSingleThreadSession.NBitcoinSerialize(block);

            Assert.IsTrue(block.ToBytes().SequenceEqual(result));
        }

        [TestMethod]
        public void NBitcoinSerializeWithuint256ReturnsAsBytes()
        {
            uint256 uInt = new uint256();

            var result = DBreezeSingleThreadSession.NBitcoinSerialize(uInt);

            Assert.IsTrue(uInt.ToBytes().SequenceEqual(result));
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void NBitcoinSerializeWithUnsupportedObjectThrowsException()
        {
            string test = "Should throw exception.";

            DBreezeSingleThreadSession.NBitcoinSerialize(test);            
        }

        [TestMethod]
        public void NBitcoinDeserializeWithCoinsDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var coins = new Coins(genesis.Transactions[0], 0);

            var result = (Coins)DBreezeSingleThreadSession.NBitcoinDeserialize(coins.ToBytes(), typeof(Coins));

            Assert.AreEqual(coins.CoinBase, result.CoinBase);
            Assert.AreEqual(coins.Height, result.Height);
            Assert.AreEqual(coins.IsEmpty, result.IsEmpty);
            Assert.AreEqual(coins.IsPruned, result.IsPruned);
            Assert.AreEqual(coins.Outputs.Count, result.Outputs.Count);
            Assert.AreEqual(coins.Outputs[0].ScriptPubKey.Hash, result.Outputs[0].ScriptPubKey.Hash);
            Assert.AreEqual(coins.Outputs[0].Value, result.Outputs[0].Value);
            Assert.AreEqual(coins.UnspentCount, result.UnspentCount);
            Assert.AreEqual(coins.Value, result.Value);
            Assert.AreEqual(coins.Version, result.Version);
        }

        [TestMethod]
        public void NBitcoinDeserializeWithBlockHeaderDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var blockHeader = genesis.Header;

            var result = (BlockHeader)DBreezeSingleThreadSession.NBitcoinDeserialize(blockHeader.ToBytes(), typeof(BlockHeader));

            Assert.AreEqual(blockHeader.GetHash(), result.GetHash());
        }

        [TestMethod]
        public void NBitcoinDeserializeWithRewindDataDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var rewindData = new RewindData(genesis.GetHash());

            var result = (RewindData)DBreezeSingleThreadSession.NBitcoinDeserialize(rewindData.ToBytes(), typeof(RewindData));

            Assert.AreEqual(genesis.GetHash(), result.PreviousBlockHash);
        }

        [TestMethod]
        public void NBitcoinDeserializeWithuint256DeserializesObject()
        {
            var val = uint256.One;

            var result = (uint256)DBreezeSingleThreadSession.NBitcoinDeserialize(val.ToBytes(), typeof(uint256));

            Assert.AreEqual(val, result);
        }

        [TestMethod]
        public void NBitcoinDeserializeWithBlockDeserializesObject()
        {
            var network = Network.RegTest;
            var block = network.GetGenesis();

            var result = (Block)DBreezeSingleThreadSession.NBitcoinDeserialize(block.ToBytes(), typeof(Block));

            Assert.AreEqual(block.GetHash(), result.GetHash());
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void NBitcoinDeserializeWithNotSupportedClassThrowsException()
        {
            string test = "Should throw exception.";

            DBreezeSingleThreadSession.NBitcoinDeserialize(Encoding.UTF8.GetBytes(test), typeof(string));         
        }

        [TestMethod]
        public void DoRunsSameThreadAsSessionCreated()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoRunsSameThreadAsSessionCreated")))
            {
                session.Do(() =>
                {
                    Assert.AreEqual("TestThread", Thread.CurrentThread.Name);
                });
            }
        }

        [TestMethod]
        public void DoWithTypeRunsSameThreadAsSessionCreated()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoWithTypeRunsSameThreadAsSessionCreated")))
            {
                var thread = session.Do<string>(() =>
                {
                    return Thread.CurrentThread.Name;
                });
                thread.Wait();

                Assert.AreEqual("TestThread", thread.Result);
            }
        }

        [TestMethod]
        public void DoStartsTransaction()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoStartsTransaction")))
            {
                session.Do(() =>
                {
                    Assert.IsNotNull(session.Transaction);
                });
            }
        }

        [TestMethod]
        public void DoAbleToAccessExistingTransactionData()
        {            
            var dir = AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoAbleToAccessExistingTransactionData");
            uint256[] data = SetupTransactionData(dir);

            using (var session = new DBreezeSingleThreadSession("TestThread", dir))
            {
                session.Do(() =>
                {
                    var data2 = new uint256[data.Length];
                    var transaction = session.Transaction;
                    int i = 0;
                    foreach (var row in transaction.SelectForward<byte[], byte[]>("Table"))
                    {
                        data2[i++] = new uint256(row.Key, false);
                    }

                    Assert.IsTrue(data.SequenceEqual(data2));
                });
            }
        }

        [TestMethod]
        public void DoWithTypePerformsTask()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoWithTypePerformsTask")))
            {
                var task = session.Do<string>(() =>
                {
                    return "TaskResult";

                });
                task.Wait();

                Assert.AreEqual("TaskResult", task.Result);
            }
        }

        private static uint256[] SetupTransactionData(string folder)
        {
            using (var engine = new DBreezeEngine(folder))
            {
                var data = new[]
                          {
                        new uint256(3),
                        new uint256(2),
                        new uint256(5),
                        new uint256(10),
                    };

                using (var tx = engine.GetTransaction())
                {
                    foreach (var d in data)
                        tx.Insert("Table", d.ToBytes(false), d.ToBytes());
                    tx.Commit();
                }

                return data;
            }
        }
    }
}
