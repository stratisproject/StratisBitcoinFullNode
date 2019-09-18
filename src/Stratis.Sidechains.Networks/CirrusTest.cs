using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.SmartContracts.Networks.Policies;

namespace Stratis.Sidechains.Networks
{
    /// <summary>
    /// Right now, ripped nearly straight from <see cref="PoANetwork"/>.
    /// </summary>
    public class CirrusTest : PoANetwork
    {
        /// <summary> The name of the root folder containing the different federated peg blockchains.</summary>
        private const string NetworkRootFolderName = "cirrus";

        /// <summary> The default name used for the federated peg configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "fedpeg.conf";

        internal CirrusTest()
        {
            this.Name = "CirrusTest";
            this.NetworkType = NetworkType.Testnet;
            this.CoinTicker = "TCRS";
            this.Magic = 0x522357B;
            this.DefaultPort = 26179;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 26175;
            this.DefaultAPIPort = 38223;
            this.DefaultSignalRPort = 39823;
            this.MaxTipAge = 768; // 20% of the fastest time it takes for one MaxReorgLength of blocks to be mined.
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.DefaultBanTimeSeconds = 1920; // 240 (MaxReorg) * 16 (TargetSpacing) / 2 = 32 Minutes

            var consensusFactory = new SmartContractCollateralPoAConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1556631753;
            this.GenesisNonce = 146421;
            this.GenesisBits = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            string coinbaseText = "https://github.com/stratisproject/StratisBitcoinFullNode/tree/master/src/Stratis.CirrusD";
            Block genesisBlock = CirrusNetwork.CreateGenesis(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward, coinbaseText);

            this.Genesis = genesisBlock;

            // Configure federation public keys used to sign blocks.
            // Keep in mind that order in which keys are added to this list is important
            // and should be the same for all nodes operating on this network.
            var genesisFederationMembers = new List<IFederationMember>()
            {
                new CollateralFederationMember(new PubKey("03cfc06ef56352038e1169deb3b4fa228356e2a54255cf77c271556d2e2607c28c"), new Money(50000_00000000), "TH9DJ8nRRpS8y2upTmtyqjKwe5WxvARUnV"),
                new CollateralFederationMember(new PubKey("022553fb641898be98e6e331d644c1689455536e58ad643d84844e981708da38e9"), new Money(50000_00000000), "TTEpaHVm7DZktv6ELbeMjDprhAFeFx72zq"),
                new CollateralFederationMember(new PubKey("02fc828e06041ae803ab5378b5ec4e0def3d4e331977a69e1b6ef694d67f5c9c13"), new Money(50000_00000000), "TEsYkDkL5HCJuynJu7gMDHq9bzfgsASQQr"),
                new CollateralFederationMember(new PubKey("02fd4f3197c40d41f9f5478d55844f522744258ca4093b5119571de1a5df1bc653"), new Money(50000_00000000), "TKwaUhwEWTKEXFSKfPECmdLfwQnG7Np6uL"),
                new CollateralFederationMember(new PubKey("030ac8e3e119257aff4512ea44450632a6a9b54104f936732d31c28a63a2104064"), new Money(50000_00000000), "TX8dS5SbDrXj8XpxEVRTbquDaD61f2JmAG"),
                new CollateralFederationMember(new PubKey("03348a438f86727c579febfd6a656cfd6477605e5fa00efa5b4f5fe1cab01c49ef"), new Money(50000_00000000), "TMTkMb6MFET5WQfDuLdR8SG2Y7z4fJsvVU"),
                new CollateralFederationMember(new PubKey("024142689f38fdb5e8faf3bc7bc5065ecaad6be93a34055ffce0554f9268639c98"), new Money(50000_00000000), "TQ4amrotnc9FePCaKaTPew8jAud3XuWSJG"),
                new CollateralFederationMember(new PubKey("03382ceb0a59b9b922aca6be9959ae51dabda159e79465393a308ee267ecebcaa5"), new Money(50000_00000000), "TQxQP3kWkJ5xqoqvdGjz7PhS2Xow7e1YTd"),
                new CollateralFederationMember(new PubKey("027d31e9dc3ee5a42b1273ae8184e716fc616fed6d7b62323fa0a33901d188cfeb"), new Money(50000_00000000), "TCukNecugPJCVxn6PE1GFevXhQvB5dcw8F"),
                new CollateralFederationMember(new PubKey("02ef5a8167276ade598460e0c102cb216071e8430d55f10788979d8820fe2440b6"), new Money(50000_00000000), "TDpQVrN5j5jK8a5HwsfqGukkhPRVU5mUtK"),
                new CollateralFederationMember(new PubKey("03d8e88797b56894a0d8ce6421defd4572fc8d19e18321d07ea22a6adec59f7fd1"), new Money(50000_00000000), "TTCVR57PDiKLduuLAyJoe8fca1qhEHG9ok"),
                new CollateralFederationMember(new PubKey("0357c1f34d11e6a93d4e158e109ed5309ae77981a4968c23c975aa7640fe913429"), new Money(50000_00000000), "TQAWF82FHFo539DmZrDBo2UnMEnnpXoV6J"),
                new CollateralFederationMember(new PubKey("0260c88a2fed5b615abcbde67e62762e2aa224460bd7918ca9d9f42ddfc1f63d08"), new Money(50000_00000000), "TL4gRvnqDtbuUYKg73QgaEyYAPryWcSLnc"),
                new CollateralFederationMember(new PubKey("020432898887bcc515b20d5d3dcea0ee86c700a3279c8d773caf37b5c317b4e2b4"), new Money(50000_00000000), "TMG96zyTMGVcaXgJntq7JZmKx2csJx7h75"),
                new CollateralFederationMember(new PubKey("02f9b73070474b7cfb3e6c2624c069cdbd211954f82862505f10cf0a2c3a45e7c5"), new Money(50000_00000000), "TB2Cnd34fauqheApsJS1PKemDDVV4QRYGW"),
            };

            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5,
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 16,
                votingEnabled: true,
                autoKickIdleMembers: false
            )
            {
                EnforceMinProtocolVersionAtBlockHeight = 505900, // setting the value to zero makes the functionality inactive
                EnforcedMinProtocolVersion = NBitcoin.Protocol.ProtocolVersion.CIRRUS_VERSION // minimum protocol version which will be enforced at block height defined in EnforceMinProtocolVersionAtBlockHeight
            };

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new NoBIP9Deployments();

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 400,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 240, // Heuristic. Roughly 2 * mining members
                defaultAssumeValid: null,
                maxMoney: Money.Coins(20_000_000),
                coinbaseMaturity: 1,
                premineHeight: 2,
                premineReward: Money.Coins(20_000_000),
                proofOfWorkReward: Money.Coins(0),
                powTargetTimespan: TimeSpan.FromDays(14), // two weeks
                powTargetSpacing: TimeSpan.FromMinutes(1),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: true,
                powLimit: null,
                minimumChainWork: null,
                isProofOfStake: false,
                lastPowBlock: 0,
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero
            );

            // Same as current smart contracts test networks to keep tests working
            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 127 }; // t
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 137 }; // x
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            Bech32Encoder encoder = Encoders.Bech32("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("cirrustest1.stratisplatform.com", "cirrustest1.stratisplatform.com")

            };

            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new SmartContractsStandardScriptsRegistry();

            // 16 below should be changed to TargetSpacingSeconds when we move that field.
            Assert(this.DefaultBanTimeSeconds <= this.Consensus.MaxReorgLength * 16 / 2);
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0000af9ab2c8660481328d0444cf167dfd31f24ca2dbba8e5e963a2434cffa93"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("cf8ce1419bbc4870b7d4f1c084534d91126dd3283b51ec379e0a20e27bd23633"));
        }
    }
}
