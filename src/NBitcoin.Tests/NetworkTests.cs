using System;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.BouncyCastle.Math;
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
        public void BitcoinMainnetIsInitializedCorrectly()
        {
            Network network = Network.Main;

            Assert.Equal(6, network.DNSSeeds.Count);
            Assert.Equal(512, network.SeedNodes.Count);

            Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(227931, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(388381, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(363725, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Equal(new uint256("0x0000000000000000000000000000000000000000002cb971dd56d1c583c20f90"), network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1199145601), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1230767999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1462060800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1479168000), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1510704000), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
            Assert.Equal(0, network.Consensus.CoinType);
			Assert.False(network.Consensus.IsProofOfStake);
            Assert.Equal(new uint256("0x000000000000000000174f783cc20c1415f90c4d17c9a5bcd06ba67207c9bc80"), network.Consensus.DefaultAssumeValid);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinTestnetIsInitializedCorrectly()
        {
            Network network = Network.TestNet;

            Assert.Equal(3, network.DNSSeeds.Count);
            Assert.Empty(network.SeedNodes);

            Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(51, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(75, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(100, network.Consensus.MajorityWindow);
            Assert.Equal(21111, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(581885, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(330776, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8"), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Equal(new uint256("0x0000000000000000000000000000000000000000000000198b4def2baa9338d6"), network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.True(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1512, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1199145601), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1230767999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1456790400), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1462060800), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
            Assert.Equal(1, network.Consensus.CoinType);
			Assert.False(network.Consensus.IsProofOfStake);
            Assert.Equal(new uint256("0x000000000000015682a21fc3b1e5420435678cba99cace2b07fe69b668467651"), network.Consensus.DefaultAssumeValid);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinRegTestIsInitializedCorrectly()
        {
            Network network = Network.RegTest;

            Assert.Empty(network.DNSSeeds);
            Assert.Empty(network.SeedNodes);

            Assert.Equal(150, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(100000000, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(100000000, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(100000000, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256(), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Equal(uint256.Zero, network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.True(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.True(network.Consensus.PowNoRetargeting);
            Assert.Equal(108, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(144, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(999999999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(999999999), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(BIP9DeploymentsParameters.AlwaysActive), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(999999999), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
            Assert.Equal(0, network.Consensus.CoinType);
			Assert.False(network.Consensus.IsProofOfStake);
            Assert.Null(network.Consensus.DefaultAssumeValid);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisMainIsInitializedCorrectly()
        {
            Network network = Network.StratisMain;

            Assert.Equal(4, network.DNSSeeds.Count);
            Assert.Equal(3, network.SeedNodes.Count);

			Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(227931, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(388381, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(363725, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
			Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1199145601), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1230767999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1462060800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
			Assert.Equal(12500, network.Consensus.LastPOWBlock);
			Assert.True(network.Consensus.IsProofOfStake);
			Assert.Equal(105, network.Consensus.CoinType);
			Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
			Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
			Assert.Equal(new uint256("0x55a8205ae4bbf18f4d238c43f43005bd66e0b1f679b39e2c5c62cf6903693a5e"), network.Consensus.DefaultAssumeValid);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisTestnetIsInitializedCorrectly()
        {
            Network network = Network.StratisTest;

            Assert.Equal(4, network.DNSSeeds.Count);
            Assert.Equal(4, network.SeedNodes.Count);

			Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(227931, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(388381, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(363725, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1199145601), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1230767999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1462060800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
			Assert.Equal(12500, network.Consensus.LastPOWBlock);
			Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(105, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
			Assert.Equal(new uint256("0x98fa6ef0bca5b431f15fd79dc6f879dc45b83ed4b1bbe933a383ef438321958e"), network.Consensus.DefaultAssumeValid);
         
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisRegTestIsInitializedCorrectly()
        {
            Network network = Network.StratisRegTest;

            Assert.Empty(network.DNSSeeds);
            Assert.Empty(network.SeedNodes);

			Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(227931, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(388381, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(363725, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.True(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.True(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Equal(28, network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1199145601), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1230767999), network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy].Timeout);
            Assert.Equal(0, network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(1462060800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(1493596800), network.Consensus.BIP9Deployments[BIP9Deployments.CSV].Timeout);
            Assert.Equal(1, network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Bit);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].StartTime);
            Assert.Equal(Utils.UnixTimeToDateTime(0), network.Consensus.BIP9Deployments[BIP9Deployments.Segwit].Timeout);
            Assert.Equal(12500, network.Consensus.LastPOWBlock);
            Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(105, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
            Assert.Null(network.Consensus.DefaultAssumeValid);
         
            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }
    }
}
