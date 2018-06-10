using System;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanGetNetworkFromName()
        {
            Network bitcoinMain = Network.Main;
            Network bitcoinTestnet = Network.TestNet;
            Network bitcoinRegtest = Network.RegTest;
            Network stratisMain = Network.StratisMain;
            Network stratisTestnet = Network.StratisTest;
            Network stratisRegtest = Network.StratisRegTest;

            Assert.Equal(Network.GetNetwork("main"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("mainnet"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("MainNet"), bitcoinMain);
            Assert.Equal(Network.GetNetwork("test"), bitcoinTestnet);
            Assert.Equal(Network.GetNetwork("testnet"), bitcoinTestnet);
            Assert.Equal(Network.GetNetwork("regtest"), bitcoinRegtest);
            Assert.Equal(Network.GetNetwork("reg"), bitcoinRegtest);
            Assert.Equal(Network.GetNetwork("stratismain"), stratisMain);
            Assert.Equal(Network.GetNetwork("StratisMain"), stratisMain);
            Assert.Equal(Network.GetNetwork("StratisTest"), stratisTestnet);
            Assert.Equal(Network.GetNetwork("stratistest"), stratisTestnet);
            Assert.Equal(Network.GetNetwork("StratisRegTest"), stratisRegtest);
            Assert.Equal(Network.GetNetwork("stratisregtest"), stratisRegtest);
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

            Assert.Equal(15, network.Checkpoints.Count);
            Assert.Equal(6, network.DNSSeeds.Count);
            Assert.Equal(512, network.SeedNodes.Count);

            Assert.Equal(Network.GetNetwork("main"), network);
            Assert.Equal(Network.GetNetwork("mainnet"), network);

            Assert.Equal("Main", network.Name);
            Assert.Equal(Network.BitcoinRootFolderName, network.RootFolderName);
            Assert.Equal(Network.BitcoinDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0xD9B4BEF9, network.Magic);
            Assert.Equal(new PubKey(Encoders.Hex.DecodeData("04fc9702847840aaf195de8442ebecedf5b095cdbb9bc716bda9110971b28a49e0ead8564ff0db22209e0374782c093bb899692d524e9d6a6956e7c5ecbcd68284")), network.AlertPubKey);
            Assert.Equal(8333, network.DefaultPort);
            Assert.Equal(8332, network.RPCPort);
            Assert.Equal(Network.BitcoinMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.BitcoinDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(1000, network.MinTxFee);
            Assert.Equal(20000, network.FallbackFee);
            Assert.Equal(1000, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());
            
            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { 0 }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (5) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xB2), (0x1E) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xAD), (0xE4) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2a }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 23 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

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
            Assert.Equal(100, network.Consensus.CoinbaseMaturity);
            Assert.Equal(0, network.Consensus.PremineReward);
            Assert.Equal(0, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(50), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Zero, network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)0, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinTestnetIsInitializedCorrectly()
        {
            Network network = Network.TestNet;

            Assert.Equal(2, network.Checkpoints.Count);
            Assert.Equal(3, network.DNSSeeds.Count);
            Assert.Empty(network.SeedNodes);

            Assert.Equal("TestNet", network.Name);
            Assert.Equal(Network.BitcoinRootFolderName, network.RootFolderName);
            Assert.Equal(Network.BitcoinDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x0709110B.ToString(), network.Magic.ToString());
            Assert.Equal(new PubKey(Encoders.Hex.DecodeData("04302390343f91cc401d56d68b123028bf52e5fca1939df127f63c6467cdf9c8e2c14b61104cf817d0b780da337893ecc4aaff1309e536162dabbdb45200ca2b0a")), network.AlertPubKey);
            Assert.Equal(18333, network.DefaultPort);
            Assert.Equal(18332, network.RPCPort);
            Assert.Equal(Network.BitcoinMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.BitcoinDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(1000, network.MinTxFee);
            Assert.Equal(20000, network.FallbackFee);
            Assert.Equal(1000, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("tb").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("tb").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { 111 }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (239) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x35), (0x87), (0xCF) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x35), (0x83), (0x94) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2b }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 115 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

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
            Assert.Equal(100, network.Consensus.CoinbaseMaturity);
            Assert.Equal(0, network.Consensus.PremineReward);
            Assert.Equal(0, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(50), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Zero, network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)0, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void BitcoinRegTestIsInitializedCorrectly()
        {
            Network network = Network.RegTest;

            Assert.Empty(network.Checkpoints);
            Assert.Empty(network.DNSSeeds);
            Assert.Empty(network.SeedNodes);

            Assert.Equal("RegTest", network.Name);
            Assert.Equal(Network.BitcoinRootFolderName, network.RootFolderName);
            Assert.Equal(Network.BitcoinDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0xDAB5BFFA, network.Magic);
            Assert.Equal(18444, network.DefaultPort);
            Assert.Equal(18332, network.RPCPort);
            Assert.Equal(Network.BitcoinMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.BitcoinDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(1000, network.MinTxFee);
            Assert.Equal(20000, network.FallbackFee);
            Assert.Equal(1000, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("tb").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("tb").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { 111 }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (239) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x35), (0x87), (0xCF) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x35), (0x83), (0x94) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2b }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 115 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

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
            Assert.Equal(100, network.Consensus.CoinbaseMaturity);
            Assert.Equal(0, network.Consensus.PremineReward);
            Assert.Equal(0, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(50), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Zero, network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)0, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisMainIsInitializedCorrectly()
        {
            Network network = Network.StratisMain;

            Assert.Equal(25, network.Checkpoints.Count);
            Assert.Equal(4, network.DNSSeeds.Count);
            Assert.Equal(3, network.SeedNodes.Count);

            Assert.Equal("StratisMain", network.Name);
            Assert.Equal(Network.StratisRootFolderName, network.RootFolderName);
            Assert.Equal(Network.StratisDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x5223570.ToString(), network.Magic.ToString());
            Assert.Equal(16178, network.DefaultPort);
            Assert.Equal(16174, network.RPCPort);
            Assert.Equal(Network.StratisMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.StratisDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(10000, network.MinTxFee);
            Assert.Equal(60000, network.FallbackFee);
            Assert.Equal(10000, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (63) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (125) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (63 + 128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xB2), (0x1E) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xAD), (0xE4) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2a }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 23 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

            Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
			Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.CSV]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.Segwit]);
			Assert.Equal(12500, network.Consensus.LastPOWBlock);
			Assert.True(network.Consensus.IsProofOfStake);
			Assert.Equal(105, network.Consensus.CoinType);
			Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
			Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
			Assert.Equal(new uint256("0x55a8205ae4bbf18f4d238c43f43005bd66e0b1f679b39e2c5c62cf6903693a5e"), network.Consensus.DefaultAssumeValid);
            Assert.Equal(50, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(98000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(4), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(1), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisTestnetIsInitializedCorrectly()
        {
            Network network = Network.StratisTest;

            Assert.Equal(10, network.Checkpoints.Count);
            Assert.Equal(4, network.DNSSeeds.Count);
            Assert.Equal(4, network.SeedNodes.Count);
            
            Assert.Equal("StratisTest", network.Name);
            Assert.Equal(Network.StratisRootFolderName, network.RootFolderName);
            Assert.Equal(Network.StratisDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x11213171.ToString(), network.Magic.ToString());
            Assert.Equal(26178, network.DefaultPort);
            Assert.Equal(26174, network.RPCPort);
            Assert.Equal(Network.StratisMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.StratisDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(10000, network.MinTxFee);
            Assert.Equal(60000, network.FallbackFee);
            Assert.Equal(10000, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (65) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (65 + 128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xB2), (0x1E) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xAD), (0xE4) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2a }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 23 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

            Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.CSV]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.Segwit]);
			Assert.Equal(12500, network.Consensus.LastPOWBlock);
			Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(105, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
			Assert.Equal(new uint256("0x98fa6ef0bca5b431f15fd79dc6f879dc45b83ed4b1bbe933a383ef438321958e"), network.Consensus.DefaultAssumeValid);
            Assert.Equal(10, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(98000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(4), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(1), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void StratisRegTestIsInitializedCorrectly()
        {
            Network network = Network.StratisRegTest;

            Assert.Empty(network.Checkpoints);
            Assert.Empty(network.DNSSeeds);
            Assert.Empty(network.SeedNodes);

            Assert.Equal("StratisRegTest", network.Name);
            Assert.Equal(Network.StratisRootFolderName, network.RootFolderName);
            Assert.Equal(Network.StratisDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0xefc0f2cd, network.Magic);
            Assert.Equal(18444, network.DefaultPort);
            Assert.Equal(18442, network.RPCPort);
            Assert.Equal(Network.StratisMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(Network.StratisDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(0, network.MinTxFee);
            Assert.Equal(0, network.FallbackFee);
            Assert.Equal(0, network.MinRelayTxFee);

            Assert.Equal(2, network.Bech32Encoders.Length);
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (65) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (65 + 128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
            Assert.Equal(new byte[] { 0x01, 0x42 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC]);
            Assert.Equal(new byte[] { 0x01, 0x43 }, network.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xB2), (0x1E) }, network.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY]);
            Assert.Equal(new byte[] { (0x04), (0x88), (0xAD), (0xE4) }, network.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY]);
            Assert.Equal(new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 }, network.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE]);
            Assert.Equal(new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A }, network.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE]);
            Assert.Equal(new byte[] { 0x2a }, network.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS]);
            Assert.Equal(new byte[] { 23 }, network.Base58Prefixes[(int)Base58Type.ASSET_ID]);
            Assert.Equal(new byte[] { 0x13 }, network.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS]);

            Assert.Equal(210000, network.Consensus.SubsidyHalvingInterval);
            Assert.Equal(750, network.Consensus.MajorityEnforceBlockUpgrade);
            Assert.Equal(950, network.Consensus.MajorityRejectBlockOutdated);
            Assert.Equal(1000, network.Consensus.MajorityWindow);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP34]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP65]);
            Assert.Equal(0, network.Consensus.BuriedDeployments[BuriedDeployments.BIP66]);
            Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
			Assert.Equal(new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.True(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.True(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.TestDummy]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.CSV]);
            Assert.Null(network.Consensus.BIP9Deployments[BIP9Deployments.Segwit]);
            Assert.Equal(12500, network.Consensus.LastPOWBlock);
            Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(105, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
            Assert.Null(network.Consensus.DefaultAssumeValid);
            Assert.Equal(10, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(98000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(4), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(1), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void MineGenesisBlockWithMissingParametersThrowsException()
        {
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(null, "some string", new Target(new uint256()), Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "", new Target(new uint256()), Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "some string", null, Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "some string", new Target(new uint256()), null));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void MineGenesisBlockWithLongCoinbaseTextThrowsException()
        {
            string coinbaseText100Long = "1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111";
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), coinbaseText100Long, new Target(new uint256()), Money.Zero));
        }
    }
}