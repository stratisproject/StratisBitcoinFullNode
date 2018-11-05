using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using City.Networks;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Networks;
//using NBitcoin.NetworkDefinitions;
using Xunit;

namespace City.Chain.Tests
{
    public class NetworkTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanGetNetworkFromName()
        {
            //Network cityMain = Networks.CityMain;
            //Network cityRegtest = Networks.CityRegTest;
            //Network cityTestnet = Networks.CityTest;

            //Assert.Equal(NetworksContainer.GetNetwork("citymain"), cityMain);
            //Assert.Equal(NetworksContainer.GetNetwork("CityMain"), cityMain);
            //Assert.Equal(NetworksContainer.GetNetwork("CityRegTest"), cityRegtest);
            //Assert.Equal(NetworksContainer.GetNetwork("cityregtest"), cityRegtest);
            //Assert.Equal(NetworksContainer.GetNetwork("CityTest"), cityTestnet);
            //Assert.Equal(NetworksContainer.GetNetwork("citytest"), cityTestnet);
            //Assert.Null(NetworksContainer.GetNetwork("invalid"));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ReadMagicByteWithFirstByteDuplicated()
        {
            var network = new CityMain();
            List<byte> bytes = network.MagicBytes.ToList();
            bytes.Insert(0, bytes.First());

            using (var memstrema = new MemoryStream(bytes.ToArray()))
            {
                bool found = network.ReadMagic(memstrema, new CancellationToken());
                Assert.True(found);
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CityMainIsInitializedCorrectly()
        {
            Network network = new CityMain();

            Assert.Equal(1, network.Checkpoints.Count);
            Assert.Equal(4, network.DNSSeeds.Count);
            Assert.Equal(4, network.SeedNodes.Count);

            Assert.Equal("CityMain", network.Name);
            Assert.Equal(CityMain.CityRootFolderName, network.RootFolderName);
            Assert.Equal(CityMain.CityDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x43545901.ToString(), network.Magic.ToString());
            Assert.Equal(4333, network.DefaultPort);
            Assert.Equal(4334, network.RPCPort);
            Assert.Equal(CityMain.CityMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(CityMain.CityDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(10000, network.MinTxFee);
            Assert.Equal(10000, network.FallbackFee);
            Assert.Equal(10000, network.MinRelayTxFee);
            Assert.Equal("CITY", network.CoinTicker);

            Assert.Equal(2, network.Bech32Encoders.Length);
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (28) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (88) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (237) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
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
            //Assert.Equal(new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[CityBIP9Deployments.TestDummy]);
            Assert.Equal(2500, network.Consensus.LastPOWBlock);
            Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(1926, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
            Assert.Equal(new uint256("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"), network.Consensus.DefaultAssumeValid);
            Assert.Equal(50, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(13736000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(2), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(20), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);
            Assert.Equal(long.MaxValue, network.Consensus.MaxMoney);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0xb3425d46594a954b141898c7eebe369c6e6a35d2dab393c1f495504d2147883b"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void GenerateWitnessAddressAndVerify()
        {
            List<Network> networks = new List<Network>();
            networks.Add(new CityMain());
            networks.Add(new BitcoinMain());
            networks.Add(new StratisMain());

            foreach (Network network in networks)
            {
                var privateKey = new Key();
                BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(network);
                var witnessAddress = privateKey.PubKey.WitHash.ToString();
                var scriptPubKey = address.ScriptPubKey.ToString();
                //Assert.StartsWith("C", address.ToString());

                BitcoinSecret secret = privateKey.GetWif(network);
                var wif = secret.ToWif();
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void GenerateSomeCityMainNetAddressAndVerifyPrefix()
        {
            Network network = new CityMain();

            for (int i = 0; i < 10; i++)
            {
                var privateKey = new Key();
                BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(network);
                Assert.StartsWith("C", address.ToString());

                var witnessAddress = privateKey.PubKey.WitHash.ToString();
                var test = address.ScriptPubKey.ToString();

                BitcoinSecret secret = privateKey.GetWif(network);
                var wif = secret.ToWif();
                Assert.StartsWith("c", wif.ToString());
            }

            for (int i = 0; i < 10; i++)
            {
                var privateKey = new Key(false);
                BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(network);
                Assert.StartsWith("C", address.ToString());

                var witnessAddress = privateKey.PubKey.WitHash.ToString();
                var test = address.ScriptPubKey.ToString();

                BitcoinSecret secret = privateKey.GetWif(network);
                var wif = secret.ToWif();
                Assert.StartsWith("8", wif.ToString());
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void GenerateSomeCityTestNetAddressAndVerifyPrefix()
        {
            Network network = new CityTest();

            for (int i = 0; i < 10; i++)
            {
                var privateKey = new Key();
                BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(network);
                Assert.StartsWith("T", address.ToString());

                var witnessAddress = privateKey.PubKey.WitHash.ToString();
                var test = address.ScriptPubKey.ToString();

                BitcoinSecret secret = privateKey.GetWif(network);
                var wif = secret.ToWif();
                Assert.StartsWith("V", wif.ToString());
            }

            for (int i = 0; i < 10; i++)
            {
                var privateKey = new Key(false);
                BitcoinPubKeyAddress address = privateKey.PubKey.GetAddress(network);
                Assert.StartsWith("T", address.ToString());

                var witnessAddress = privateKey.PubKey.WitHash.ToString();
                var test = address.ScriptPubKey.ToString();

                BitcoinSecret secret = privateKey.GetWif(network);
                var wif = secret.ToWif();
                Assert.StartsWith("7", wif.ToString());
            }
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CityTestnetIsInitializedCorrectly()
        {
            Network network = new CityTest();

            Assert.Equal(3, network.Checkpoints.Count);
            Assert.Equal(3, network.DNSSeeds.Count);
            Assert.Equal(3, network.SeedNodes.Count);

            Assert.Equal("CityTest", network.Name);
            Assert.Equal(CityMain.CityRootFolderName, network.RootFolderName);
            Assert.Equal(CityMain.CityDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x43545401.ToString(), network.Magic.ToString());
            Assert.Equal(24333, network.DefaultPort);
            Assert.Equal(24334, network.RPCPort);
            Assert.Equal(CityMain.CityMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(CityMain.CityDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(10000, network.MinTxFee);
            Assert.Equal(10000, network.FallbackFee);
            Assert.Equal(10000, network.MinRelayTxFee);
            Assert.Equal("TCITY", network.CoinTicker);

            Assert.Equal(2, network.Bech32Encoders.Length);
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (66) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (66 + 128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
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
            Assert.Equal(new uint256("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.False(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.False(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[CityBIP9Deployments.TestDummy]);
            Assert.Equal(125000, network.Consensus.LastPOWBlock);
            Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(1926, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
            Assert.Equal(new uint256("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"), network.Consensus.DefaultAssumeValid);
            Assert.Equal(10, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(13736000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(2), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(20), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);
            Assert.Equal(long.MaxValue, network.Consensus.MaxMoney);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x034aaae9ca8e297078a2fed80cdaaf72ffad8aa1b1988c7b6edd8f01d69312ca"), genesis.Header.HashMerkleRoot);
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CityRegTestIsInitializedCorrectly()
        {
            Network network = new CityRegTest();

            Assert.Empty(network.Checkpoints);
            Assert.Equal(3, network.DNSSeeds.Count);
            Assert.Equal(3, network.SeedNodes.Count);

            Assert.Equal("CityRegTest", network.Name);
            Assert.Equal(CityMain.CityRootFolderName, network.RootFolderName);
            Assert.Equal(CityMain.CityDefaultConfigFilename, network.DefaultConfigFilename);
            Assert.Equal(0x43525901.ToString(), network.Magic.ToString());
            Assert.Equal(14333, network.DefaultPort);
            Assert.Equal(14334, network.RPCPort);
            Assert.Equal(CityMain.CityMaxTimeOffsetSeconds, network.MaxTimeOffsetSeconds);
            Assert.Equal(CityMain.CityDefaultMaxTipAgeInSeconds, network.MaxTipAge);
            Assert.Equal(0, network.MinTxFee);
            Assert.Equal(0, network.FallbackFee);
            Assert.Equal(0, network.MinRelayTxFee);
            Assert.Equal("TCITY", network.CoinTicker);

            Assert.Equal(2, network.Bech32Encoders.Length);
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].ToString());
            //Assert.Equal(new Bech32Encoder("bc").ToString(), network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].ToString());

            Assert.Equal(12, network.Base58Prefixes.Length);
            Assert.Equal(new byte[] { (66) }, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS]);
            Assert.Equal(new byte[] { (196) }, network.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS]);
            Assert.Equal(new byte[] { (66 + 128) }, network.Base58Prefixes[(int)Base58Type.SECRET_KEY]);
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
            Assert.Equal(new uint256("0x0000da5d40883d6c8aade797d8d6dcbf5cbc8e6428569170da39d2f01e8290e5"), network.Consensus.BIP34Hash);
            Assert.Equal(new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), network.Consensus.PowLimit);
            Assert.Null(network.Consensus.MinimumChainWork);
            Assert.Equal(TimeSpan.FromSeconds(14 * 24 * 60 * 60), network.Consensus.PowTargetTimespan);
            Assert.Equal(TimeSpan.FromSeconds(10 * 60), network.Consensus.PowTargetSpacing);
            Assert.True(network.Consensus.PowAllowMinDifficultyBlocks);
            Assert.True(network.Consensus.PowNoRetargeting);
            Assert.Equal(1916, network.Consensus.RuleChangeActivationThreshold);
            Assert.Equal(2016, network.Consensus.MinerConfirmationWindow);
            Assert.Null(network.Consensus.BIP9Deployments[CityBIP9Deployments.TestDummy]);
            Assert.Equal(125000, network.Consensus.LastPOWBlock);
            Assert.True(network.Consensus.IsProofOfStake);
            Assert.Equal(1926, network.Consensus.CoinType);
            Assert.Equal(new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimit);
            Assert.Equal(new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)), network.Consensus.ProofOfStakeLimitV2);
            Assert.Null(network.Consensus.DefaultAssumeValid);
            Assert.Equal(10, network.Consensus.CoinbaseMaturity);
            Assert.Equal(Money.Coins(13736000000), network.Consensus.PremineReward);
            Assert.Equal(2, network.Consensus.PremineHeight);
            Assert.Equal(Money.Coins(2), network.Consensus.ProofOfWorkReward);
            Assert.Equal(Money.Coins(20), network.Consensus.ProofOfStakeReward);
            Assert.Equal((uint)500, network.Consensus.MaxReorgLength);
            Assert.Equal(long.MaxValue, network.Consensus.MaxMoney);

            Block genesis = network.GetGenesis();
            Assert.Equal(uint256.Parse("0x0000da5d40883d6c8aade797d8d6dcbf5cbc8e6428569170da39d2f01e8290e5"), genesis.GetHash());
            Assert.Equal(uint256.Parse("0x49f8ad9e1d47aec09a38b7b54e282ed0ba30099b8632152931be74e2865266d5"), genesis.Header.HashMerkleRoot);
        }
    }
}
