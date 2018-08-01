using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Networks
{
    public class BitcoinTest : BitcoinMain
    {
        public BitcoinTest()
        {
            this.Name = "TestNet";
            this.AdditionalNames = new List<string> { "test" };
            this.Magic = 0x0709110B;
            this.DefaultPort = 18333;
            this.RPCPort = 18332;
            this.CoinTicker = "TBTC";

            // Taken from BitcoinMain Consensus options
            var consensus = new Consensus();
            consensus.SubsidyHalvingInterval = 210000;
            consensus.PowLimit = new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.PowNoRetargeting = false;
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            consensus.CoinbaseMaturity = 100;
            consensus.PremineReward = Money.Zero;
            consensus.ProofOfWorkReward = Money.Coins(50);
            consensus.ProofOfStakeReward = Money.Zero;
            consensus.MaxReorgLength = 0;
            consensus.MaxMoney = 21000000 * Money.COIN;
            
            // BitcoinTest differences
            consensus.MajorityEnforceBlockUpgrade = 51;
            consensus.MajorityRejectBlockOutdated = 75;
            consensus.MajorityWindow = 100;
            consensus.BIP34Hash = new uint256("0x0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8");
            consensus.MinimumChainWork = new uint256("0x0000000000000000000000000000000000000000000000198b4def2baa9338d6");
            consensus.DefaultAssumeValid = new uint256("0x000000000000015682a21fc3b1e5420435678cba99cace2b07fe69b668467651"); // 1292762
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 21111;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 581885;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 330776;
            consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
            consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1456790400, 1493596800);
            consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1462060800, 1493596800);
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.RuleChangeActivationThreshold = 1512; // 75% for testchains
            consensus.CoinType = 1;

            consensus.Options = new ConsensusOptions(); // Default - set to Bitcoin params.

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };

            var encoder = new Bech32Encoder("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // Partially obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L246
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 546, new CheckpointInfo(new uint256("000000002a936ca763904c3c35fce2f3556c559c0214345d31b1bcebf76acb70")) },
                { 100_000, new CheckpointInfo(new uint256("00000000009e2958c15ff9290d571bf9459e93b19765c6801ddeccadbb160a1e")) },
                { 200_000, new CheckpointInfo(new uint256("0000000000287bffd321963ef05feab753ebe274e1d78b2fd4e2bfe9ad3aa6f2")) },
                { 300_000, new CheckpointInfo(new uint256("000000000000226f7618566e70a2b5e020e29579b46743f05348427239bf41a1")) },
                { 400_000, new CheckpointInfo(new uint256("000000000598cbbb1e79057b79eef828c495d4fc31050e6b179c57d07d00367c")) },
                { 500_000, new CheckpointInfo(new uint256("000000000001a7c0aaa2630fbb2c0e476aafffc60f82177375b2aaa22209f606")) },
                { 600_000, new CheckpointInfo(new uint256("000000000000624f06c69d3a9fe8d25e0a9030569128d63ad1b704bbb3059a16")) },
                { 700_000, new CheckpointInfo(new uint256("000000000000406178b12a4dea3b27e13b3c4fe4510994fd667d7c1e6a3f4dc1")) },
                { 800_000, new CheckpointInfo(new uint256("0000000000209b091d6519187be7c2ee205293f25f9f503f90027e25abf8b503")) },
                { 900_000, new CheckpointInfo(new uint256("0000000000356f8d8924556e765b7a94aaebc6b5c8685dcfa2b1ee8b41acd89b")) },
                { 1_000_000, new CheckpointInfo(new uint256("0000000000478e259a3eda2fafbeeb0106626f946347955e99278fe6cc848414")) },
                { 1_210_000, new CheckpointInfo(new uint256("00000000461201277cf8c635fc10d042d6f0a7eaa57f6c9e8c099b9e0dbc46dc")) }
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("bitcoin.petertodd.org", "testnet-seed.bitcoin.petertodd.org"),
                new DNSSeedData("bluematt.me", "testnet-seed.bluematt.me"),
                new DNSSeedData("bitcoin.schildbach.de", "testnet-seed.bitcoin.schildbach.de")
            };

            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1296688602;
            this.GenesisNonce = 414098458;
            this.GenesisBits = 0x1d00ffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            this.Genesis = CreateBitcoinGenesisBlock(consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            consensus.HashGenesisBlock = this.Genesis.GetHash();

            this.Consensus = consensus;

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"));
        }
    }
}