using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using HashLib;
using Moq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Crypto;
using Xunit;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    [Collection("SidechainIdentifierTests")]
    public class Network_Shall
    {
        private TestAssets testAssets = new TestAssets();

        [Fact(Skip = "Fails after nuget update. The definition of testAssets needs reviewing.")]
        //This test might not be entirely predictable because the code could get the wrong network setting
        //from NetworkContainer if the network has already been registered.
        public void use_provider_to_create_genesis_hash()
        {
            var sidechainInfoProvider = new Mock<ISidechainInfoProvider>();
            sidechainInfoProvider.Setup(m => m.GetSidechainInfo(TestAssets.EnigmaChainName))
                .Returns(this.testAssets.GetSidechainInfo(TestAssets.EnigmaChainName, 0));

            using (var sidechainIdentifier = SidechainIdentifier.Create(TestAssets.EnigmaChainName, sidechainInfoProvider.Object))
            {
                var network = SidechainNetwork.SidechainMain;
                network.Name.Should().Be(SidechainNetwork.SidechainMainName);
                network.DefaultPort.Should().Be(36000);
                network.RPCPort.Should().Be(36100);
                network.GenesisHash.Should()
                    .Be(uint256.Parse("03ca8b76093da3e9132a6f1002ce9e95468ae538f10ea5eb23c594265e3bcbea"));
            }
        }

        [Fact(Skip = "Fails after nuget update. The definition of testAssets needs reviewing.")]
        public void cache_network()
        {
            var sidechainInfoProvider = new Mock<ISidechainInfoProvider>();
            sidechainInfoProvider.Setup(m => m.GetSidechainInfo(TestAssets.EnigmaChainName))
                .Returns(this.testAssets.GetSidechainInfo(TestAssets.EnigmaChainName, 0));

            using (var sidechainIdentifier = SidechainIdentifier.Create(TestAssets.EnigmaChainName, sidechainInfoProvider.Object))
            {
                var network = SidechainNetwork.SidechainMain;
                network.Name.Should().Be(SidechainNetwork.SidechainMainName);
                network.DefaultPort.Should().Be(36000);
            }

            //now change the Sidechain info by using a different seed
            sidechainInfoProvider.Setup(m => m.GetSidechainInfo(TestAssets.EnigmaChainName))
                .Returns(this.testAssets.GetSidechainInfo(TestAssets.EnigmaChainName, 1));

            using (var sidechainIdentifier = SidechainIdentifier.Create(TestAssets.EnigmaChainName, sidechainInfoProvider.Object))
            {
                var network = SidechainNetwork.SidechainMain;
                network.Name.Should().Be(SidechainNetwork.SidechainMainName);

                //you might expect that this would fail and the value should be (36001) because of the seed.
                //however the first network above, was previously cached and we don't get a new network.
                //See NetworkContainer in the Network class in NBitcoin where this behavior is 'defined'.
                network.DefaultPort.Should().Be(36000);
            }
        }

        [Fact(Skip = "Fails after nuget update. The definition of testAssets needs reviewing.")]
        public void correctly_generate_genesis_hash()
        {
            Block genesis =
                SidechainNetwork.CreateSidechainGenesisBlock(new PosConsensusFactory(), 1510170036, 2500036, 0x1e0fffff, 1, Money.Zero);
            uint256 ui1 = genesis.GetHash();
            genesis.Header.Time = 1510170036;
            genesis.Header.Nonce = 2500036;
            genesis.Header.Bits =
                new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            uint256 ui = genesis.GetHash();
            ui.Should().Be(uint256.Parse("41abc054d831276ac59d733c82839a94d644d605bbf8197f442f164809e3d049"));
            genesis.Transactions[0].GetHash().Should()
                .Be(uint256.Parse("1bc7c2c62f36afb4cbdab73836764a91233a3882845418b04d010f903bd590db"));
        }

        [Fact]
        public void convert_datetime_from_unixtime()
        {
            //8th Nov 2017 7.40pm
            uint time = 1510170000;
            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(time);
            dateTimeOffset.Year.Should().Be(2017);
            dateTimeOffset.Month.Should().Be(11);
            dateTimeOffset.Hour.Should().Be(19);
            dateTimeOffset.ToUnixTimeSeconds().Should().Be(time);
        }

        [Fact]
        public void allow_magic_to_be_expressed_in_bytes()
        {
            var networkInfo = new NetworkInfo("netName", 1510170000, 2500036, 12345, 53, 4500, 5600, 334, "net");
            networkInfo.MessageStart.Should().Be(12345);
            networkInfo.MessageStart = 54321;
            networkInfo.MessageStart.Should().Be(54321);
            networkInfo.MessageStartAsBytes.Should().Contain(new byte[] { 0x31, 0xD4, 0x00, 0x00 });

            networkInfo.MessageStartAsBytes = new byte[]{ 0x71, 0xd4, 0xe4, 0xf5 };
            networkInfo.MessageStart.Should().Be(4125414513u);
        }
    }
}
