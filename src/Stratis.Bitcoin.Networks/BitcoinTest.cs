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

            var consensusFactory = new ConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1296688602;
            this.GenesisNonce = 414098458;
            this.GenesisBits = 0x1d00ffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            Block genesisBlock = CreateBitcoinGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 21111,
                [BuriedDeployments.BIP65] = 581885,
                [BuriedDeployments.BIP66] = 330776
            };

            var bip9Deployments = new BIP9DeploymentsArray
            {
                [BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999),
                [BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1456790400, 1493596800),
                [BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1462060800, 1493596800)
            };

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                coinType: 1,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 51,
                majorityRejectBlockOutdated: 75,
                majorityWindow: 100,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8"),
                ruleChangeActivationThreshold: 1512,
                minerConfirmationWindow: 2016,
                maxReorgLength: 0,
                defaultAssumeValid: new uint256("0x000000000000015682a21fc3b1e5420435678cba99cace2b07fe69b668467651"), // 1292762
                maxMoney: 21000000 * Money.COIN,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Coins(50),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: true,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: new uint256("0x0000000000000000000000000000000000000000000000198b4def2baa9338d6"),
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero
            );

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
                { 1210000, new CheckpointInfo(new uint256("00000000461201277cf8c635fc10d042d6f0a7eaa57f6c9e8c099b9e0dbc46dc")) }
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("bitcoin.petertodd.org", "testnet-seed.bitcoin.petertodd.org"),
                new DNSSeedData("bluematt.me", "testnet-seed.bluematt.me"),
                new DNSSeedData("bitcoin.schildbach.de", "testnet-seed.bitcoin.schildbach.de")
            };

            this.SeedNodes = new List<NetworkAddress>();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"));
        }
    }
}