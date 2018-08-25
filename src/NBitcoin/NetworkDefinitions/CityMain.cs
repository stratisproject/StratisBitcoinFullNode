using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;

namespace NBitcoin.NetworkDefinitions
{
    public class CityMain : Network
    {
        /// <summary>City maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off.</summary>
        public const int CityMaxTimeOffsetSeconds = 25 * 60;

        /// <summary>City default value for the maximum tip age in seconds to consider the node in initial block download (2 hours).</summary>
        public const int CityDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary>The name of the root folder containing the different City Chain blockchains (CityMain, CityTest, CityRegTest).</summary>
        public const string CityRootFolderName = "city";

        /// <summary>The default name used for the Stratis configuration file.</summary>
        public const string CityDefaultConfigFilename = "city.conf";

        public CityMain()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x67;
            messageStart[1] = 0x84;
            messageStart[2] = 0x89;
            messageStart[3] = 0x23;
            uint magic = BitConverter.ToUInt32(messageStart, 0); //0x23898467; 

            this.Name = "CityMain";
            this.Magic = magic;
            this.DefaultPort = 4333;
            this.RPCPort = 4334;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 60000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = CityRootFolderName;
            this.DefaultConfigFilename = CityDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = "CITY";

            this.Consensus.SubsidyHalvingInterval = 210000;
            this.Consensus.MajorityEnforceBlockUpgrade = 750;
            this.Consensus.MajorityRejectBlockOutdated = 950;
            this.Consensus.MajorityWindow = 1000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            this.Consensus.BIP34Hash = new uint256("0x000007e4aff2b770e876ac1bc2d5317f15c2505b1f8e58423febf0913bd0cc34");
            this.Consensus.PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            this.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            this.Consensus.PowAllowMinDifficultyBlocks = false;
            this.Consensus.PowNoRetargeting = false;
            this.Consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            this.Consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            this.Consensus.LastPOWBlock = 12500;
            this.Consensus.IsProofOfStake = true;
            this.Consensus.ConsensusFactory = new PosConsensusFactory();
            this.Consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            this.Consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            this.Consensus.CoinType = 4535;
            // https://bitcoincore.org/en/2017/03/08/release-0.14.0/#assumed-valid-blocks
            this.Consensus.DefaultAssumeValid = new uint256("0x000007e4aff2b770e876ac1bc2d5317f15c2505b1f8e58423febf0913bd0cc34"); // 0 TODO: Must be updated after some mining.
            this.Consensus.CoinbaseMaturity = 50;
            this.Consensus.PremineReward = Money.Coins(13736000000);
            this.Consensus.PremineHeight = 2;
            this.Consensus.ProofOfWorkReward = Money.Coins(1); // Produced up until last POW block.
            this.Consensus.ProofOfStakeReward = Money.Coins(100); // 52 560 000 a year.
            this.Consensus.MaxReorgLength = 500;
            this.Consensus.MaxMoney = long.MaxValue;

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
                //{ 0, new CheckpointInfo(new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                //{ 2, new CheckpointInfo(new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff"), new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e")) }, // Premine
                //{ 50, new CheckpointInfo(new uint256("0x0353b43f4ce80bf24578e7c0141d90d7962fb3a4b4b4e5a17925ca95e943b816"), new uint256("0x7c2af3b10d13f9d2bc6063baaf7f0860d90d870c994378144f9bf85d0d555061")) },
                //{ 100, new CheckpointInfo(new uint256("0x688468a8aa48cd1c2197e42e7d8acd42760b7e2ac4bcab9d18ac149a673e16f6"), new uint256("0xcf2b1e9e76aaa3d96f255783eb2d907bf6ccb9c1deeb3617149278f0e4a1ab1b")) },
                //{ 150, new CheckpointInfo(new uint256("0xe4ae9663519abec15e28f68bdb2cb89a739aee22f53d1573048d69141db6ee5d"), new uint256("0xa6c17173e958dc716cc0892ce33dad8bc327963d78a16c436264ceae43d584ce")) },
                //{ 127500, new CheckpointInfo(new uint256("0x4773ca7512489df22de03aa03938412fab5b46154b05df004b97bcbeaa184078"), new uint256("0x619743c02ebaff06b90fcc5c60c52dba8aa3fdb6ba3800aae697cbb3c5483f17")) },
                //{ 128943, new CheckpointInfo(new uint256("0x36bcaa27a53d3adf22b2064150a297adb02ac39c24263a5ceb73856832d49679"), new uint256("0xa3a6fd04e41fcaae411a3990aaabcf5e086d2d06c72c849182b27b4de8c2c42a")) },
                //{ 136601, new CheckpointInfo(new uint256("0xf5c5210c55ff1ef9c04715420a82728e1647f3473e31dc478b3745a97b4a6d10"), new uint256("0x42058fabe21f7b118a9e358eaf9ef574dadefd024244899e71f2f6d618161e16")) }, // Hardfork to V2 - Drifting Bug Fix
                //{ 170000, new CheckpointInfo(new uint256("0x22b10952e0cf7e85bfc81c38f1490708f195bff34d2951d193cc20e9ca1fc9d5"), new uint256("0xa4942a6c99cba397cf2b18e4b912930fe1e64a7413c3d97c5a926c2af9073091")) },
                //{ 200000, new CheckpointInfo(new uint256("0x2391dd493be5d0ff0ef57c3b08c73eefeecc2701b80f983054bb262f7a146989"), new uint256("0x253152d129e82c30c584197deb6833502eff3ec2f30014008f75842d7bb48453")) },
                //{ 250000, new CheckpointInfo(new uint256("0x681c70fab7c1527246138f0cf937f0eb013838b929fbe9a831af02a60fc4bf55"), new uint256("0x24eed95e00c90618aa9d137d2ee273267285c444c9cde62a25a3e880c98a3685")) },
                //{ 300000, new CheckpointInfo(new uint256("0xd10ca8c2f065a49ae566c7c9d7a2030f4b8b7f71e4c6fc6b2a02509f94cdcd44"), new uint256("0x39c4dd765b49652935524248b4de4ccb604df086d0723bcd81faf5d1c2489304")) },
                //{ 350000, new CheckpointInfo(new uint256("0xe2b76d1a068c4342f91db7b89b66e0f2146d3a4706c21f3a262737bb7339253a"), new uint256("0xd1dd94985eaaa028c893687a7ddf89143dcf0176918f958c2d01f33d64910399")) },
                //{ 390000, new CheckpointInfo(new uint256("0x4682737abc2a3257fdf4c3c119deb09cbac75981969e2ffa998b4f76b7c657bb"), new uint256("0xd84b204ee94499ff65262328a428851fb4f4d2741e928cdd088fdf1deb5413b8")) },
                //{ 394000, new CheckpointInfo(new uint256("0x42857fa2bc15d45cdcaae83411f755b95985da1cb464ee23f6d40936df523e9f"), new uint256("0x2314b336906a2ed2a39cbdf6fc0622530709c62dbb3a3729de17154fc9d1a7c4")) },
                //{ 400000, new CheckpointInfo(new uint256("0x4938d5cf450b4e2d9072558971223555055aa3987b634a8bb2e97f95d1a3c501"), new uint256("0x1756c127f0ac7029cf095a6c3ed9b7d206d0e36744d8b3cef306002f9f901a31")) },
                //{ 450000, new CheckpointInfo(new uint256("0x7699e07ac18c25ac042deb6b985e2decfd6034cb6361de2152a2d704ef785bac"), new uint256("0xa140a86a03c4f852d8a651f6386a02a0262e7bbf841ede8b54541c011c51ba0e")) },
                //{ 500000, new CheckpointInfo(new uint256("0x558700d99239e64017d10910466719fe1edc6f863bd3de254b89ba828818ea47"), new uint256("0x6a0b7dab4a7aa9ea2477cddffe5a976c9423454835054a39c19d37613002638f")) },
                //{ 550000, new CheckpointInfo(new uint256("0x83d074957f509772b1fbbfaeb7bdc52932c540d54e205b92a7d4e92f68957eb4"), new uint256("0x012b63ad7d50606f2cafb1a7806ea90f4981c56b5407725aeeff34e3c584433c")) },
                //{ 600000, new CheckpointInfo(new uint256("0xcd05c75c0c47060d78508095c0766452f80e2defb6a4641ac603742a2ccf2207"), new uint256("0x1f25507e09b199a71d5879811376856e5fb3da1da3d522204c017eec3b6c4dad")) },
                //{ 650000, new CheckpointInfo(new uint256("0xa2814a439b33662f43bdbc8ab089d368524975bb53a08326395e57456cba8d39"), new uint256("0x192a2ef70e2280cf05aa5655f496a109b2445d0ddda62531e9bce9aaced1fe54")) },
                //{ 700000, new CheckpointInfo(new uint256("0x782b2506bb67bb448ff56aa946f7aad6b63a6b27d8c5818725a56b568f25b9ce"), new uint256("0xf23dc64b130d80790a83a86913f619afaeef10e1fd24e4b42af9387ec935edd6")) },
                //{ 750000, new CheckpointInfo(new uint256("0x4db98bd41a2f9ee845cc89ac03109686f615f4d0dcd81e0488005c1616fa692c"), new uint256("0x9f620af75bc27a0e4b503deaf7f052ba112a49bb74fb6446350642bc2ac9d93b")) },
                //{ 800000, new CheckpointInfo(new uint256("0x161da1d97d35d6897dbdae110617bb839805f8b02d33ac23d227a87cacbfac78"), new uint256("0xe95049a313345f26bfa90094ceb6400f43359fc43fc5f1471918d98bc4ab3bac")) },
                //{ 850000, new CheckpointInfo(new uint256("0xc3a249b01795b22858aa00fd0973471fcd769a14f4f9cf0abe6651ac3e6ade19"), new uint256("0x5de8766ed4cfcc3ce9d74f38196596c6f91b9ff62cbd20abbfa991dca54d2bd4")) }
            };

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("node1.city-chain.org", "node1.city-chain.org"),
                new DNSSeedData("node1.citychain.foundation", "node1.citychain.foundation"),
                new DNSSeedData("10.0.0.122", "10.0.0.122"),
                new DNSSeedData("10.0.0.192", "10.0.0.192")
            };

            string[] seedNodes = {
                "10.0.0.122", // city-chain.org (node3)
                "10.0.0.192", // citychain.foundation (node3)
                "10.0.0.122", // City 1 Node #1
                "10.0.0.192" // City 2 Node #1
                // As more cities are included in the City Chain, consider adding their official nodes to the seeds.
            };
            this.SeedNodes = ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            // Create the genesis block.
            this.GenesisTime = 1533081600; // 1 August 2018
            this.GenesisNonce = 162758;
            this.GenesisBits = 0x1E0FFFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2018-07-27: "Bitcoin’s roots are in anarcho-capitalism, a movement that aspires to reproduce the mechanisms of the free market without the need for banks or state bodies to enforce rules."
            // https://www.newscientist.com/article/mg23831841-200-how-to-think-about-the-blockchain/
            string pszTimestamp = "July 27, 2018, New Scientiest, Bitcoin’s roots are in anarcho-capitalism";

            this.Genesis = CreateCityGenesisBlock(this.Consensus.ConsensusFactory, pszTimestamp, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000007e4aff2b770e876ac1bc2d5317f15c2505b1f8e58423febf0913bd0cc34"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x40ba87eb3e03731abe7f2c7643c493b6383020513d5352334c6e0ff343e2f82d"));
        }

        protected static Block CreateCityGenesisBlock(ConsensusFactory consensusFactory, string pszTimestamp, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
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