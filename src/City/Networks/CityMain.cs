using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace City.Networks
{
    public class CityMain : Network
    {
        /// <summary> City maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int CityMaxTimeOffsetSeconds = 25 * 60;

        /// <summary> City default value for the maximum tip age in seconds to consider the node in initial block download (2 hours). </summary>
        public const int CityDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary> The name of the root folder containing the different City Chain blockchains (CityMain, CityTest, CityRegTest). </summary>
        public const string CityRootFolderName = "city";

        /// <summary> The default name used for the configuration file. </summary>
        public const string CityDefaultConfigFilename = "city.conf";

        public CityMain()
        {
            this.Name = "CityMain";
            this.Magic = 0x43545901; // .CTY
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultPort = 4333;
            this.RPCPort = 4334;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = CityRootFolderName;
            this.DefaultConfigFilename = CityDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = "CITY";

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1538481600; // 10/02/2018 @ 12:00pm (UTC)
            this.GenesisNonce = 1626464;
            this.GenesisBits = 0x1E0FFFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2018-07-27: "Bitcoin’s roots are in anarcho-capitalism, a movement that aspires to reproduce the mechanisms of the free market without the need for banks or state bodies to enforce rules."
            // https://www.newscientist.com/article/mg23831841-200-how-to-think-about-the-blockchain/
            string pszTimestamp = "July 27, 2018, New Scientiest, Bitcoin’s roots are in anarcho-capitalism";

            Block genesisBlock = CreateCityGenesisBlock(pszTimestamp, consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new CityBIP9Deployments()
            {
                [CityBIP9Deployments.ColdStaking] = new BIP9DeploymentsParameters(2,
                    new DateTime(2018, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc))
            };

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 1926,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                defaultAssumeValid: new uint256("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"),
                maxMoney: long.MaxValue,
                coinbaseMaturity: 50,
                premineHeight: 2,
                premineReward: Money.Coins(13736000000),
                proofOfWorkReward: Money.Coins(2), // Produced up until last POW block.
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 2500,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(20)
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (28) }; // P2PKH: C, list: https://en.bitcoin.it/wiki/List_of_address_prefixes
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (88) }; // P2SH: c
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (237) }; // WIF: c (compressed, 8 uncompressed), initial character for compressed private key in WIF format.
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0x072227af2fda8ef6a5f7a19ec3a1c6de54ddc537dd407da938766ed460e77982"), new uint256("0xe93eb6c21c65024ca06ac2f89481bdc832cab1607ed2adfeafb6c679b6a4a1f6")) },
                { 50, new CheckpointInfo(new uint256("0xce58ab37dd5965c3474c5917fcbb59aa342c6754a452e5faf87050bb6015d511"), new uint256("0xb877b17b3d7324ac1a3615a6c245c702282e5be74fd50cf25bb02bc5f2ea7944")) },
                { 100, new CheckpointInfo(new uint256("0x5edbf09aadfbdb0d74d428b002fcda197debb775955a161f2890ed844a5159da"), new uint256("0x354210eecb7ed3f8df3d384b8d615f789fdffdf3f3d4945c23e5966827010b73")) },
                { 200, new CheckpointInfo(new uint256("0x180745aeb0754cde04dac52dbb056dac5b1c665de86ee23806c1c7675dac3ac9"), new uint256("0x7f5dfec171542e5d2b56e874eb4f1e26c949e8a33a64e3fe0b06f1b5783fbd54")) },
                { 1000, new CheckpointInfo(new uint256("0xc848602fe0f33511766b66c6d5a28e11cd54e7c61d69c0898f868f46b5c9d6f7"), new uint256("0x13fad5f4e1dc5120f88e48e3f8d08bbc57b78fa960df8b600819c631f1038327")) },
                { 2000, new CheckpointInfo(new uint256("0xe73ec56e8c4159594bea6e8d73f1a5e0c980246861064451e2f0d2f609ed7d0b"), new uint256("0x569ab17ece910f954d26f3ab8a83ec5ae8952957b3f1c1868fb7b04abd52dd19")) },
                { 10000, new CheckpointInfo(new uint256("0x5fba1895ca1134f3c6fefdd45996039d45aab0177a10e86cac4990226f62ad95"), new uint256("0x47c6db50a945d8d4b17676e65a74ccd64e3b73f696082c3cdb7a9c0b9658e9cb")) },
                { 20000, new CheckpointInfo(new uint256("0xa0f81a7734e621ae2e2cecea7a3b851dee3b5a85ba4732e0867cbf5c496f04ed"), new uint256("0xf044e402b75b4314231e1498fefc8b11da8cc9cb7797e2e59b2acdbcea1b02b3")) }
            };

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("city-chain.org", "seed.city-chain.org"),
                new DNSSeedData("city-coin.org", "seed.city-coin.org"),
                new DNSSeedData("citychain.foundation", "seed.citychain.foundation"),
                new DNSSeedData("liberstad.com", "seed.liberstad.com")
            };

            this.SeedNodes = new List<NetworkAddress>
            {
                new NetworkAddress(IPAddress.Parse("52.175.194.227"), this.DefaultPort),
                new NetworkAddress(IPAddress.Parse("13.73.143.193"), this.DefaultPort),
                new NetworkAddress(IPAddress.Parse("40.115.2.6"), this.DefaultPort),
                new NetworkAddress(IPAddress.Parse("13.66.158.6"), this.DefaultPort),
            };

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x00000b0517068e602ed5279c20168cfa1e69884ee4e784909652da34c361bff2"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0xb3425d46594a954b141898c7eebe369c6e6a35d2dab393c1f495504d2147883b"));
        }

        protected static Block CreateCityGenesisBlock(string pszTimestamp, ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }
    }
}