using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanGetNetworkFromName()
        {
            Assert.Equal(Network.GetNetwork("main"), Network.Main);
            Assert.Equal(Network.GetNetwork("reg"), Network.RegTest);
            Assert.Equal(Network.GetNetwork("regtest"), Network.RegTest);
            Assert.Equal(Network.GetNetwork("testnet"), Network.TestNet);
            Assert.Null(Network.GetNetwork("invalid"));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void RegisterNetworkTwiceFails()
        {
            Network main = Network.Main;
            var error = Assert.Throws<InvalidOperationException>(() => Network.Register(main));
            Assert.Contains("is already registered", error.Message);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void RegisterNetworkTwiceWithDifferentNamesSucceeds()
        {
            Network main = Network.Main;
            Network main2 = Network.Register(main, "main2");

            Assert.Equal(Network.GetNetwork("main"), Network.GetNetwork("main2"));
        }
        
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ReadMagicByteWithFirstByteDuplicated()
        {
            var bytes = Network.Main.MagicBytes.ToList();
            bytes.Insert(0, bytes.First());

            using(var memstrema = new MemoryStream(bytes.ToArray()))
            {
                var found = Network.Main.ReadMagic(memstrema, new CancellationToken());
                Assert.True(found);
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinMainnetGenesisIsInitializedCorrectly()
        {
            Network network = Network.Main;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinTestnetGenesisIsInitializedCorrectly()
        {
            Network network = Network.TestNet;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinRegTestGenesisIsInitializedCorrectly()
        {
            Network network = Network.RegTest;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisMainGenesisIsInitializedCorrectly()
        {
            Network network = Network.StratisMain;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisTestnetGenesisIsInitializedCorrectly()
        {
            Network network = Network.StratisTest;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisRegTestGenesisIsInitializedCorrectly()
        {
            Network network = Network.StratisRegTest;
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }
    }
}
