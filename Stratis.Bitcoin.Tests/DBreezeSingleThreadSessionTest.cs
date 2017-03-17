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
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class DBreezeSingleThreadSessionTest : TestBase
    {
        public DBreezeSingleThreadSessionTest()
        {
        }

        [Fact]
        public void NBitcoinSerializeWithBitcoinSerializableReturnsAsBytes()
        {
            Block block = new Block();

            var result = DBreezeSingleThreadSession.NBitcoinSerialize(block);

            Assert.Equal(block.ToBytes(), result);
        }

        [Fact]
        public void NBitcoinSerializeWithuint256ReturnsAsBytes()
        {
            uint256 uInt = new uint256();

            var result = DBreezeSingleThreadSession.NBitcoinSerialize(uInt);

            Assert.Equal(uInt.ToBytes(), result);
        }

        [Fact]
        public void NBitcoinSerializeWithUnsupportedObjectThrowsException()
        {
            Assert.Throws(typeof(NotSupportedException), () =>
            {
                string test = "Should throw exception.";

                DBreezeSingleThreadSession.NBitcoinSerialize(test);
            });
        }

        [Fact]
        public void NBitcoinDeserializeWithCoinsDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var coins = new Coins(genesis.Transactions[0], 0);

            var result = (Coins)DBreezeSingleThreadSession.NBitcoinDeserialize(coins.ToBytes(), typeof(Coins));

            Assert.Equal(coins.Coinbase, result.Coinbase);
            Assert.Equal(coins.Height, result.Height);
            Assert.Equal(coins.IsEmpty, result.IsEmpty);
            Assert.Equal(coins.IsPruned, result.IsPruned);
            Assert.Equal(coins.Outputs.Count, result.Outputs.Count);
            Assert.Equal(coins.Outputs[0].ScriptPubKey.Hash, result.Outputs[0].ScriptPubKey.Hash);
            Assert.Equal(coins.Outputs[0].Value, result.Outputs[0].Value);
            Assert.Equal(coins.UnspentCount, result.UnspentCount);
            Assert.Equal(coins.Value, result.Value);
            Assert.Equal(coins.Version, result.Version);
        }

        [Fact]
        public void NBitcoinDeserializeWithBlockHeaderDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var blockHeader = genesis.Header;

            var result = (BlockHeader)DBreezeSingleThreadSession.NBitcoinDeserialize(blockHeader.ToBytes(), typeof(BlockHeader));

            Assert.Equal(blockHeader.GetHash(), result.GetHash());
        }

        [Fact]
        public void NBitcoinDeserializeWithRewindDataDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var rewindData = new RewindData(genesis.GetHash());

            var result = (RewindData)DBreezeSingleThreadSession.NBitcoinDeserialize(rewindData.ToBytes(), typeof(RewindData));

            Assert.Equal(genesis.GetHash(), result.PreviousBlockHash);
        }

        [Fact]
        public void NBitcoinDeserializeWithuint256DeserializesObject()
        {
            var val = uint256.One;

            var result = (uint256)DBreezeSingleThreadSession.NBitcoinDeserialize(val.ToBytes(), typeof(uint256));

            Assert.Equal(val, result);
        }

        [Fact]
        public void NBitcoinDeserializeWithBlockDeserializesObject()
        {
            var network = Network.RegTest;
            var block = network.GetGenesis();

            var result = (Block)DBreezeSingleThreadSession.NBitcoinDeserialize(block.ToBytes(), typeof(Block));

            Assert.Equal(block.GetHash(), result.GetHash());
        }

        [Fact]
        public void NBitcoinDeserializeWithNotSupportedClassThrowsException()
        {
            Assert.Throws(typeof(NotSupportedException), () =>
            {
                string test = "Should throw exception.";

                DBreezeSingleThreadSession.NBitcoinDeserialize(Encoding.UTF8.GetBytes(test), typeof(string));
            });
        }

        [Fact]
        public void DoRunsSameThreadAsSessionCreated()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoRunsSameThreadAsSessionCreated")))
            {
                session.Do(() =>
                {
                    Assert.Equal("TestThread", Thread.CurrentThread.Name);
                });
            }
        }

        [Fact]
        public void DoWithTypeRunsSameThreadAsSessionCreated()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoWithTypeRunsSameThreadAsSessionCreated")))
            {
                var thread = session.Do<string>(() =>
                {
                    return Thread.CurrentThread.Name;
                });
                thread.Wait();

                Assert.Equal("TestThread", thread.Result);
            }
        }

        [Fact]
        public void DoStartsTransaction()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoStartsTransaction")))
            {
                session.Do(() =>
                {
                    Assert.NotNull(session.Transaction);
                });
            }
        }

        [Fact]
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

                    Assert.True(data.SequenceEqual(data2));
                });
            }
        }

        [Fact]
        public void DoWithTypePerformsTask()
        {
            using (var session = new DBreezeSingleThreadSession("TestThread", AssureEmptyDir("TestData/DBreezeSingleThreadSession/DoWithTypePerformsTask")))
            {
                var task = session.Do<string>(() =>
                {
                    return "TaskResult";

                });
                task.Wait();

                Assert.Equal("TaskResult", task.Result);
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
