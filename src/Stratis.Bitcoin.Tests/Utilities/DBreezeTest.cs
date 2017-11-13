using System;
using System.Linq;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of DBreeze database and <see cref="DBreezeSerializer"/> class.
    /// </summary>
    public class DBreezeTest : TestBase
    {
        /// <summary>Provider of binary (de)serialization for data stored in the database.</summary>
        private readonly DBreezeSerializer dbreezeSerializer;

        /// <summary>
        /// Initializes the DBreeze serializer.
        /// </summary>
        public DBreezeTest()
        {
            this.dbreezeSerializer = new DBreezeSerializer();
        }

        [Fact]
        public void SerializerWithBitcoinSerializableReturnsAsBytes()
        {
            Block block = new Block();

            byte[] result = this.dbreezeSerializer.Serializer(block);

            Assert.Equal(block.ToBytes(), result);
        }

        [Fact]
        public void SerializerWithUint256ReturnsAsBytes()
        {
            uint256 val = new uint256();

            byte[] result = this.dbreezeSerializer.Serializer(val);

            Assert.Equal(val.ToBytes(), result);
        }

        [Fact]
        public void SerializerWithUnsupportedObjectThrowsException()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                string test = "Should throw exception.";

                this.dbreezeSerializer.Serializer(test);
            });
        }

        [Fact]
        public void DeserializerWithCoinsDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var coins = new Coins(genesis.Transactions[0], 0);

            var result = (Coins)this.dbreezeSerializer.Deserializer(coins.ToBytes(), typeof(Coins));

            Assert.Equal(coins.CoinBase, result.CoinBase);
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
        public void DeserializerWithBlockHeaderDeserializesObject()
        {
            var network = Network.RegTest;
            var genesis = network.GetGenesis();
            var blockHeader = genesis.Header;

            var result = (BlockHeader)this.dbreezeSerializer.Deserializer(blockHeader.ToBytes(), typeof(BlockHeader));

            Assert.Equal(blockHeader.GetHash(), result.GetHash());
        }

        [Fact]
        public void DeserializerWithRewindDataDeserializesObject()
        {
            Network network = Network.RegTest;
            Block genesis = network.GetGenesis();
            var rewindData = new RewindData(genesis.GetHash());

            var result = (RewindData)this.dbreezeSerializer.Deserializer(rewindData.ToBytes(), typeof(RewindData));

            Assert.Equal(genesis.GetHash(), result.PreviousBlockHash);
        }

        [Fact]
        public void DeserializerWithuint256DeserializesObject()
        {
            uint256 val = uint256.One;

            var result = (uint256)this.dbreezeSerializer.Deserializer(val.ToBytes(), typeof(uint256));

            Assert.Equal(val, result);
        }

        [Fact]
        public void DeserializerWithBlockDeserializesObject()
        {
            Network network = Network.RegTest;
            Block block = network.GetGenesis();

            var result = (Block)this.dbreezeSerializer.Deserializer(block.ToBytes(), typeof(Block));

            Assert.Equal(block.GetHash(), result.GetHash());
        }

        [Fact]
        public void DeserializerWithNotSupportedClassThrowsException()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                string test = "Should throw exception.";

                this.dbreezeSerializer.Deserializer(Encoding.UTF8.GetBytes(test), typeof(string));
            });
        }

        [Fact]
        public void DBreezeEngineAbleToAccessExistingTransactionData()
        {            
            var dir = AssureEmptyDir("TestData/DBreezeTest/DBreezeEngineAbleToAccessExistingTransactionData");
            uint256[] data = SetupTransactionData(dir);

            using (var engine = new DBreezeEngine(dir))
            {
                using (DBreeze.Transactions.Transaction transaction = engine.GetTransaction())
                {
                    var data2 = new uint256[data.Length];
                    int i = 0;
                    foreach (Row<int, byte[]> row in transaction.SelectForward<int, byte[]>("Table"))
                    {
                        data2[i++] = new uint256(row.Value, false);
                    }

                    Assert.True(data.SequenceEqual(data2));
                }
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

                int i = 0;
                using (DBreeze.Transactions.Transaction tx = engine.GetTransaction())
                {
                    foreach (uint256 d in data)
                        tx.Insert<int, byte[]>("Table", i++, d.ToBytes(false));

                    tx.Commit();
                }

                return data;
            }
        }
    }
}
