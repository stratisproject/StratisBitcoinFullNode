using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class CheckPointsTest
    {
        private readonly Network network;

        public CheckPointsTest()
        {
            this.network = KnownNetworks.Main;
        }

        [Fact]
        public void GetLastCheckPointHeight_WithoutConsensusSettings_ReturnsZero()
        {
            var checkpoints = new Checkpoints();

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_SettingsDisabledCheckpoints_DoesNotLoadCheckpoints()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = false });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BitcoinMainnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(550000, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BitcoinTestnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(KnownNetworks.TestNet, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(1400000, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_BitcoinRegTestNet_DoesNotLoadCheckpoints()
        {
            var checkpoints = new Checkpoints(KnownNetworks.RegTest, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_StratisMainnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(KnownNetworks.StratisMain, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(1150000, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_StratisTestnet_ReturnsLastCheckPointHeight()
        {
            var checkpoints = new Checkpoints(KnownNetworks.StratisTest, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(720000, result);
        }

        [Fact()]
        public void GetLastCheckPointHeight_StratisRegTestNet_DoesNotLoadCheckpoints()
        {
            var checkpoints = new Checkpoints(KnownNetworks.StratisRegTest, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            int result = checkpoints.GetLastCheckpointHeight();

            Assert.Equal(0, result);
        }

        [Fact]
        public void GetLastCheckPointHeight_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = false };
            var checkpoints = new Checkpoints(this.network, consensusSettings);

            int result = checkpoints.GetLastCheckpointHeight();
            Assert.Equal(0, result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.GetLastCheckpointHeight();
            Assert.Equal(550000, result);
        }

        [Fact]
        public void GetCheckPoint_WithoutConsensusSettings_ReturnsNull()
        {
            var checkpoints = new Checkpoints();

            CheckpointInfo result = checkpoints.GetCheckpoint(11111);

            Assert.Null(result);
        }

        [Fact]
        public void GetCheckPoint_CheckpointExists_PoWChain_ReturnsCheckpoint()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            CheckpointInfo result = checkpoints.GetCheckpoint(11111);

            Assert.Equal(new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"), result.Hash);
            Assert.Null(result.StakeModifierV2);
        }

        [Fact]
        public void GetCheckPoint_CheckpointExists_PoSChain_ReturnsCheckpoint()
        {
            var checkpoints = new Checkpoints(KnownNetworks.StratisMain, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            CheckpointInfo result = checkpoints.GetCheckpoint(2);

            Assert.Equal(new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff"), result.Hash);
            Assert.Equal(new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e"), result.StakeModifierV2);
        }

        [Fact]
        public void GetCheckPoint_CheckpointDoesNotExist_ReturnsNull()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            CheckpointInfo result = checkpoints.GetCheckpoint(11112);

            Assert.Null(result);
        }

        [Fact]
        public void GetCheckPoint_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = false };
            var checkpoints = new Checkpoints(this.network, consensusSettings);

            CheckpointInfo result = checkpoints.GetCheckpoint(11112);
            Assert.Null(result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.GetCheckpoint(11111);
            Assert.Equal(new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"), result.Hash);
            Assert.Null(result.StakeModifierV2);
        }

        [Fact]
        public void CheckHardened_CheckpointsEnabledAfterLoad_RetrievesCheckpointsCorrectly()
        {
            var consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = false };
            var checkpoints = new Checkpoints(this.network, consensusSettings);

            bool result = checkpoints.CheckHardened(11111, new uint256("0x0000000059e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1e")); // invalid hash
            Assert.True(result);

            consensusSettings.UseCheckpoints = true;

            result = checkpoints.CheckHardened(11111, new uint256("0x0000000059e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1e")); // invalid hash
            Assert.False(result);
        }

        [Fact]
        public void CheckHardened_CheckpointExistsWithHashAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            bool result = checkpoints.CheckHardened(11111, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"));

            Assert.True(result);
        }

        [Fact]
        public void CheckHardened_CheckpointExistsWithDifferentHashAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            bool result = checkpoints.CheckHardened(11111, new uint256("0x0000000059e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1e"));

            Assert.False(result);
        }

        [Fact]
        public void CheckHardened_CheckpointDoesNotExistAtHeight_ReturnsTrue()
        {
            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            bool result = checkpoints.CheckHardened(11112, new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e"));

            Assert.True(result);
        }

        [Fact]
        public void CheckHardened_WithoutConsensusSettings_ReturnsTrue()
        {
            var checkpoints = new Checkpoints();

            bool result = checkpoints.CheckHardened(11111, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"));

            Assert.True(result);
        }

        [Fact]
        public void VerifyCheckpoints_BitcoinMainnet()
        {
            var verifyableCheckpoints = new Dictionary<int, CheckpointInfo>
            {
                { 11111, new CheckpointInfo(new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d")) },
                { 33333, new CheckpointInfo(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6")) },
                { 74000, new CheckpointInfo(new uint256("0x0000000000573993a3c9e41ce34471c079dcf5f52a0e824a81e7f953b8661a20")) },
                { 105000, new CheckpointInfo(new uint256("0x00000000000291ce28027faea320c8d2b054b2e0fe44a773f3eefb151d6bdc97")) },
                { 134444, new CheckpointInfo(new uint256("0x00000000000005b12ffd4cd315cd34ffd4a594f430ac814c91184a0d42d2b0fe")) },
                { 168000, new CheckpointInfo(new uint256("0x000000000000099e61ea72015e79632f216fe6cb33d7899acb35b75c8303b763")) },
                { 193000, new CheckpointInfo(new uint256("0x000000000000059f452a5f7340de6682a977387c17010ff6e6c3bd83ca8b1317")) },
                { 210000, new CheckpointInfo(new uint256("0x000000000000048b95347e83192f69cf0366076336c639f9b7228e9ba171342e")) },
                { 216116, new CheckpointInfo(new uint256("0x00000000000001b4f4b433e81ee46494af945cf96014816a4e2370f11b23df4e")) },
                { 225430, new CheckpointInfo(new uint256("0x00000000000001c108384350f74090433e7fcf79a606b8e797f065b130575932")) },
                { 250000, new CheckpointInfo(new uint256("0x000000000000003887df1f29024b06fc2200b55f8af8f35453d7be294df2d214")) },
                { 279000, new CheckpointInfo(new uint256("0x0000000000000001ae8c72a0b0c301f67e3afca10e819efa9041e458e9bd7e40")) },
                { 295000, new CheckpointInfo(new uint256("0x00000000000000004d9b4ef50f0f9d686fd69db2e03af35a100370c64632a983")) },
                { 486000, new CheckpointInfo(new uint256("0x000000000000000000a2a8104d61651f76c666b70754d6e9346176385f7afa24")) },
                { 491800, new CheckpointInfo(new uint256("0x000000000000000000d80de1f855902b50941bc3a3d0f71064d9613fd3943dc4")) },
            };

            var checkpoints = new Checkpoints(this.network, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            VerifyCheckpoints(checkpoints, verifyableCheckpoints);
        }

        [Fact]
        public void VerifyCheckpoints_BitcoinTestnet()
        {
            var verifyableCheckpoints = new Dictionary<int, CheckpointInfo>
            {
                { 546, new CheckpointInfo(new uint256("000000002a936ca763904c3c35fce2f3556c559c0214345d31b1bcebf76acb70")) },
                { 1210000, new CheckpointInfo(new uint256("00000000461201277cf8c635fc10d042d6f0a7eaa57f6c9e8c099b9e0dbc46dc")) },
            };

            var checkpoints = new Checkpoints(KnownNetworks.TestNet, new ConsensusSettings(NodeSettings.Default(this.network)) { UseCheckpoints = true });

            VerifyCheckpoints(checkpoints, verifyableCheckpoints);
        }

        [Fact]
        public void VerifyCheckpoints_StratisMainnet()
        {
            var verifyableCheckpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff"), new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e")) }, // Premine
                { 50, new CheckpointInfo(new uint256("0x0353b43f4ce80bf24578e7c0141d90d7962fb3a4b4b4e5a17925ca95e943b816"), new uint256("0x7c2af3b10d13f9d2bc6063baaf7f0860d90d870c994378144f9bf85d0d555061")) },
                { 100, new CheckpointInfo(new uint256("0x688468a8aa48cd1c2197e42e7d8acd42760b7e2ac4bcab9d18ac149a673e16f6"), new uint256("0xcf2b1e9e76aaa3d96f255783eb2d907bf6ccb9c1deeb3617149278f0e4a1ab1b")) },
                { 150, new CheckpointInfo(new uint256("0xe4ae9663519abec15e28f68bdb2cb89a739aee22f53d1573048d69141db6ee5d"), new uint256("0xa6c17173e958dc716cc0892ce33dad8bc327963d78a16c436264ceae43d584ce")) },
                { 127500, new CheckpointInfo(new uint256("0x4773ca7512489df22de03aa03938412fab5b46154b05df004b97bcbeaa184078"), new uint256("0x619743c02ebaff06b90fcc5c60c52dba8aa3fdb6ba3800aae697cbb3c5483f17")) },
                { 128943, new CheckpointInfo(new uint256("0x36bcaa27a53d3adf22b2064150a297adb02ac39c24263a5ceb73856832d49679"), new uint256("0xa3a6fd04e41fcaae411a3990aaabcf5e086d2d06c72c849182b27b4de8c2c42a")) },
                { 136601, new CheckpointInfo(new uint256("0xf5c5210c55ff1ef9c04715420a82728e1647f3473e31dc478b3745a97b4a6d10"), new uint256("0x42058fabe21f7b118a9e358eaf9ef574dadefd024244899e71f2f6d618161e16")) }, // Hardfork to V2 - Drifting Bug Fix
                { 170000, new CheckpointInfo(new uint256("0x22b10952e0cf7e85bfc81c38f1490708f195bff34d2951d193cc20e9ca1fc9d5"), new uint256("0xa4942a6c99cba397cf2b18e4b912930fe1e64a7413c3d97c5a926c2af9073091")) },
                { 200000, new CheckpointInfo(new uint256("0x2391dd493be5d0ff0ef57c3b08c73eefeecc2701b80f983054bb262f7a146989"), new uint256("0x253152d129e82c30c584197deb6833502eff3ec2f30014008f75842d7bb48453")) },
                { 250000, new CheckpointInfo(new uint256("0x681c70fab7c1527246138f0cf937f0eb013838b929fbe9a831af02a60fc4bf55"), new uint256("0x24eed95e00c90618aa9d137d2ee273267285c444c9cde62a25a3e880c98a3685")) },
                { 300000, new CheckpointInfo(new uint256("0xd10ca8c2f065a49ae566c7c9d7a2030f4b8b7f71e4c6fc6b2a02509f94cdcd44"), new uint256("0x39c4dd765b49652935524248b4de4ccb604df086d0723bcd81faf5d1c2489304")) },
                { 350000, new CheckpointInfo(new uint256("0xe2b76d1a068c4342f91db7b89b66e0f2146d3a4706c21f3a262737bb7339253a"), new uint256("0xd1dd94985eaaa028c893687a7ddf89143dcf0176918f958c2d01f33d64910399")) },
                { 390000, new CheckpointInfo(new uint256("0x4682737abc2a3257fdf4c3c119deb09cbac75981969e2ffa998b4f76b7c657bb"), new uint256("0xd84b204ee94499ff65262328a428851fb4f4d2741e928cdd088fdf1deb5413b8")) },
                { 394000, new CheckpointInfo(new uint256("0x42857fa2bc15d45cdcaae83411f755b95985da1cb464ee23f6d40936df523e9f"), new uint256("0x2314b336906a2ed2a39cbdf6fc0622530709c62dbb3a3729de17154fc9d1a7c4")) },
                { 400000, new CheckpointInfo(new uint256("0x4938d5cf450b4e2d9072558971223555055aa3987b634a8bb2e97f95d1a3c501"), new uint256("0x1756c127f0ac7029cf095a6c3ed9b7d206d0e36744d8b3cef306002f9f901a31")) },
                { 450000, new CheckpointInfo(new uint256("0x7699e07ac18c25ac042deb6b985e2decfd6034cb6361de2152a2d704ef785bac"), new uint256("0xa140a86a03c4f852d8a651f6386a02a0262e7bbf841ede8b54541c011c51ba0e")) },
                { 500000, new CheckpointInfo(new uint256("0x558700d99239e64017d10910466719fe1edc6f863bd3de254b89ba828818ea47"), new uint256("0x6a0b7dab4a7aa9ea2477cddffe5a976c9423454835054a39c19d37613002638f")) },
                { 550000, new CheckpointInfo(new uint256("0x83d074957f509772b1fbbfaeb7bdc52932c540d54e205b92a7d4e92f68957eb4"), new uint256("0x012b63ad7d50606f2cafb1a7806ea90f4981c56b5407725aeeff34e3c584433c")) },
                { 600000, new CheckpointInfo(new uint256("0xcd05c75c0c47060d78508095c0766452f80e2defb6a4641ac603742a2ccf2207"), new uint256("0x1f25507e09b199a71d5879811376856e5fb3da1da3d522204c017eec3b6c4dad")) },
                { 650000, new CheckpointInfo(new uint256("0xa2814a439b33662f43bdbc8ab089d368524975bb53a08326395e57456cba8d39"), new uint256("0x192a2ef70e2280cf05aa5655f496a109b2445d0ddda62531e9bce9aaced1fe54")) },
                { 700000, new CheckpointInfo(new uint256("0x782b2506bb67bb448ff56aa946f7aad6b63a6b27d8c5818725a56b568f25b9ce"), new uint256("0xf23dc64b130d80790a83a86913f619afaeef10e1fd24e4b42af9387ec935edd6")) },
                { 750000, new CheckpointInfo(new uint256("0x4db98bd41a2f9ee845cc89ac03109686f615f4d0dcd81e0488005c1616fa692c"), new uint256("0x9f620af75bc27a0e4b503deaf7f052ba112a49bb74fb6446350642bc2ac9d93b")) },
                { 800000, new CheckpointInfo(new uint256("0x161da1d97d35d6897dbdae110617bb839805f8b02d33ac23d227a87cacbfac78"), new uint256("0xe95049a313345f26bfa90094ceb6400f43359fc43fc5f1471918d98bc4ab3bac")) },
                { 850000, new CheckpointInfo(new uint256("0xc3a249b01795b22858aa00fd0973471fcd769a14f4f9cf0abe6651ac3e6ade19"), new uint256("0x5de8766ed4cfcc3ce9d74f38196596c6f91b9ff62cbd20abbfa991dca54d2bd4")) }
            };

            var checkpoints = new Checkpoints(KnownNetworks.StratisMain, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            VerifyCheckpoints(checkpoints, verifyableCheckpoints);
        }

        [Fact]
        public void VerifyCheckpoints_StratisTestnet()
        {
            var verifyableCheckpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0x56959b1c8498631fb0ca5fe7bd83319dccdc6ac003dccb3171f39f553ecfa2f2"), new uint256("0x13f4c27ca813aefe2d9018077f8efeb3766796b9144fcc4cd51803bf4376ab02")) },
                { 50000, new CheckpointInfo(new uint256("0xb42c18eacf8fb5ed94eac31943bd364451d88da0fd44cc49616ffea34d530ad4"), new uint256("0x824934ddc5f935e854ac59ae7f5ed25f2d29a7c3914cac851f3eddb4baf96d78")) },
                { 100000, new CheckpointInfo(new uint256("0xf9e2f7561ee4b92d3bde400d251363a0e8924204c326da7f4ad9ccc8863aad79"), new uint256("0xdef8d92d20becc71f662ee1c32252aca129f1bf4744026b116d45d9bfe67e9fb")) },
                { 150000, new CheckpointInfo(new uint256("0x08b7c20a450252ddf9ce41dbeb92ecf54932beac9090dc8250e933ad3a175381"), new uint256("0xf05dad15f733ae0acbd34adc449be9429099dbee5fa9ecd8e524cf28e9153adb")) },
                { 200000, new CheckpointInfo(new uint256("0x8609cc873222a0573615788dc32e377b88bfd6a0015791f627d969ee3a415115"), new uint256("0xfa28c1f20a8162d133607c6a1c8997833befac3efd9076567258a7683ac181fa")) },
                { 250000, new CheckpointInfo(new uint256("0xdd664e15ac679a6f3b96a7176303956661998174a697ad8231f154f1e32ff4a3"), new uint256("0x19fc0fa29418f8b19cbb6557c1c79dfd0eff6779c0eaaec5d245c5cdf3c96d78")) },
                { 300000, new CheckpointInfo(new uint256("0x2409eb5ae72c80d5b37c77903d75a8e742a33843ab633935ce6e5264db962e23"), new uint256("0xf5ec7af55516b8e264ed280e9a5dba0180a4a9d3713351bfea275b18f3f1514e")) },
                { 350000, new CheckpointInfo(new uint256("0x36811041e9060f4b4c26dc20e0850dca5efaabb60618e3456992e9c0b1b2120e"), new uint256("0xbfda55ef0756bcee8485e15527a2b8ca27ca877aa09c88e363ef8d3253cdfd1c")) },
                { 400000, new CheckpointInfo(new uint256("0xb6abcb933d3e3590345ca5d3abb697461093313f8886568ac8ae740d223e56f6"), new uint256("0xfaf5fcebee3ec0df5155393a99da43de18b12e620fef5edb111a791ecbfaa63a")) }
            };

            var checkpoints = new Checkpoints(KnownNetworks.StratisTest, new ConsensusSettings(NodeSettings.Default(KnownNetworks.StratisTest)) { UseCheckpoints = true });

            VerifyCheckpoints(checkpoints, verifyableCheckpoints);
        }

        private void VerifyCheckpoints(Checkpoints checkpoints, Dictionary<int, CheckpointInfo> checkpointValues)
        {
            foreach (KeyValuePair<int, CheckpointInfo> checkpoint in checkpointValues)
            {
                CheckpointInfo result = checkpoints.GetCheckpoint(checkpoint.Key);

                Assert.Equal(checkpoint.Value.Hash, result.Hash);
                Assert.Equal(checkpoint.Value.StakeModifierV2, result.StakeModifierV2);
            }
        }
    }
}
