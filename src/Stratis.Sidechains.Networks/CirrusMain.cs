using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.SmartContracts.Networks.Policies;

namespace Stratis.Sidechains.Networks
{
    /// <summary>
    /// <see cref="PoANetwork"/>.
    /// </summary>
    public class CirrusMain : PoANetwork
    {
        /// <summary> The name of the root folder containing the different federated peg blockchains.</summary>
        private const string NetworkRootFolderName = "cirrus";

        /// <summary> The default name used for the federated peg configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "cirrus.conf";

        internal CirrusMain()
        {
            this.Name = "CirrusMain";
            this.NetworkType = NetworkType.Mainnet;
            this.CoinTicker = "CRS";
            this.Magic = 0x522357AC;
            this.DefaultPort = 16179;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 16175;
            this.DefaultAPIPort = 37223;
            this.DefaultSignalRPort = 38823;
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
            this.GenesisTime = 1561982325;
            this.GenesisNonce = 3038481;
            this.GenesisBits = new Target(new uint256("00000fffff000000000000000000000000000000000000000000000000000000"));
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            string coinbaseText = "https://github.com/stratisproject/StratisBitcoinFullNode";
            Block genesisBlock = CirrusNetwork.CreateGenesis(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward, coinbaseText);

            this.Genesis = genesisBlock;

            // Configure federation public keys used to sign blocks.
            // Keep in mind that order in which keys are added to this list is important
            // and should be the same for all nodes operating on this network.
            var genesisFederationMembers = new List<IFederationMember>()
            {

                new CollateralFederationMember(new PubKey("03f5de5176e29e1e7d518ae76c1e020b1da18b57a3713ac81b16015026e232748e"), new Money(50000_00000000),"SNuLYcPoSmY1tmp9X9TRwXoF861sVMe9dP"),
                new CollateralFederationMember(new PubKey("021043aacac5c8805e3bc62eb40e8d3c04070c56b21032d4bb14200ed6e4facf93"), new Money(50000_00000000),"ScrS22tPNxL2q1Q8u9bFPX29WwWfnmTZJ6"),
                new CollateralFederationMember(new PubKey("0323033679aa439a0388f09f2883bf1ca6f50283b41bfeb6be6ddcc4e420144c16"), new Money(50000_00000000),"SVAKFx4yndzKEh2Q6o5fz5ZGADBXzFayQ4"),
                new CollateralFederationMember(new PubKey("037b5f0a88a477d9fba812826a3bf43104ca078fc51b62c0eaad15d0f9a724a4b2"), new Money(50000_00000000),"SeHbzFEC1CXco4TKTKkBbsfFMBhDyDm8Qa"),
                new CollateralFederationMember(new PubKey("027e793fbf4f6d07de15b0aa8355f88759b8bdf92a9ffb8a65a87fa8ee03baeccd"), new Money(50000_00000000),"Si8U3J659YRBAoDQYu4KduYCeNUE2bhRYo"),
                new CollateralFederationMember(new PubKey("028e1d9fd64b84a2ec85fac7185deb2c87cc0dd97270cf2d8adc3aa766dde975a7"), new Money(50000_00000000),"SfQvAArHGMVtj5AwygED9Jz6KCMYox1tvq"),
                new CollateralFederationMember(new PubKey("03535a285d0919a9bd71df3b274cecb46e16b78bf50d3bf8b0a3b41028cf8a842d"), new Money(50000_00000000),"SXGUJiniVGt77wzmeeVxFRzX8huBsL1PEA"),
                new CollateralFederationMember(new PubKey("0200c70e46cd94012caaae3fcc124e5f280f63a29cd2b3e15c15bac9d371da1e0d"), new Money(50000_00000000),"SkWeFZGkD71qsQF6hPbgMUz4v53JP3FfMo"),
                new CollateralFederationMember(new PubKey("03eb5db0b1703ea7418f0ad20582bf8de0b4105887d232c7724f43f19f14862488"), new Money(50000_00000000),"SkDJi8QmuPiTrnqnuTrLU41yesYeBQRvzV"),
                new CollateralFederationMember(new PubKey("03d8b5580b7ec709c006ef497327db27ea323bd358ca45412171c644214483b74f"), new Money(50000_00000000),"SNw49otqojNsozwnv63CxMwnCnvxZtdPBM"),
                new CollateralFederationMember(new PubKey("02ace4fbe6a622cdfc922a447c3253e8635f3fecb69241f73629e6f0596a567907"), new Money(50000_00000000),"SaQoQdEvj4VdwW526CjEQj1CTiwU5svu5m"),
                new CollateralFederationMember(new PubKey("03e8809be396745434ee8c875089e518a3eef40e31ade81869ce9cbef63484996d"), new Money(50000_00000000),"SManifHryS5bhr2WQWbUp2EVw8aT46PDSh"),
                new CollateralFederationMember(new PubKey("03a37019d2e010b046ef9d0459e4844a015758007602ddfbdc9702534924a23695"), new Money(50000_00000000),"SZUNS9RWoAHLWHq3BGKihSTHYwuVeDDGzv"),
                new CollateralFederationMember(new PubKey("0336312e7dce4f9ff8449a5d7d140be26eea7849f8ba13bb07b57b154a74aa7600"), new Money(50000_00000000),"SUMMi8UuoEUEVc5ecr9TEBaKpf152oNz4M"),
                new CollateralFederationMember(new PubKey("038e1a76f0e33474144b61e0796404821a5150c00b05aad8a1cd502c865d8b5b92"), new Money(50000_00000000),"SNMUVtVkmzvNSiM2Kbc8ykVY43Wky8iKTY"),
                new CollateralFederationMember(new PubKey("0306441cb6eb5fcd36a6af2972804382f2dc601150f6ecb773f988c3a1b1eea778"), new Money(10000_00000000),"SXhWwe72GTj8c2peaLRvqfJq9Ew2GA6wgY"),
                new CollateralFederationMember(new PubKey("02dfd2c5502c2d9fef90ec80c7912588900fb3626d46473b842a9e82ac28649991"), new Money(10000_00000000),"SbZ5FNdvbHrFCuh771j9xnCtpo2K4y522z"),
                new CollateralFederationMember(new PubKey("038670251efd386121d3110716addb73fa452fa2891cb88ac14417682366358673"), new Money(10000_00000000),"Sb651Tkgvmv2W1sfyJpXdMjxWtGHGpinHy"),
                new CollateralFederationMember(new PubKey("02e96ce15caea22e6a38a8c2b06a788f8ac28453ebb77a6578d5f394296cbc8ed4"), new Money(10000_00000000),"SVYaAjrXUsLpBnz8a8sFsAq6dJRoKDc7Qw"),
                new CollateralFederationMember(new PubKey("02b80af8dc4b20865c79228c53af6365bec92960ffdf2b2f56d7bf0555a05f647a"), new Money(10000_00000000),"Sd2AyMwkomUE9idRgroQMzTtD4pmVuaKhz"),
                new CollateralFederationMember(new PubKey("03edf8ad7419fd7223d5309ee3cfb27f2d4e6a5cd5da80aa3d225e818e7d21b9e6"), new Money(10000_00000000),"Sh5VTHYxX54ot4AM4TfiCZqXMRg26i4pZS"),
                new CollateralFederationMember(new PubKey("02674553d81d3dbcb6def93026d69bb44f738156223c342a41bda4df1503daec11"), new Money(10000_00000000),"SWN51wwcCLnpBZksXeqdP4iMkWHDKERznQ"),
                new CollateralFederationMember(new PubKey("032768540dabcbe8a78fc2916c17a07fecc51647d353e6af22a6daa3281e2d3a70"), new Money(10000_00000000),"SaaWmqqgHudmYtVnQ9YPCkBjnRepJLUzJt"),
                new CollateralFederationMember(new PubKey("02f40bd4f662ba20629a104115f0ac9ee5eab695716edfe01b240abf56e05797e2"), new Money(10000_00000000),"SUxmiBqaT6LAEwtrK9eMW98aq1LRR6fsKf"),
                new CollateralFederationMember(new PubKey("03dc030fa1c3d19ce5d464bc58440dc54f4905b766ce510e1237d906dff71c081b"), new Money(10000_00000000),"SQsYGYrCYdCPpcrwNva4m5GQ1PTdJipQ4d"),
                new CollateralFederationMember(new PubKey("03a620f0ba4f197b53ba3e8591126b54bd728ecc961607221190abb8e3cd91ea5f"), new Money(10000_00000000),"SVWia7uPjf7QkoMSuH9dZiDJbse6NXXeVy"),
                new CollateralFederationMember(new PubKey("0247e8dba42a4055f73598a57eddffb2c4db33699f258f529f1762ea29b8cc21a7"), new Money(10000_00000000),"SYTTmHq6CwGMTDKNTejq8y8HbQSQxqGqFK"),
                new CollateralFederationMember(new PubKey("029925bc527cec3592973e79b340768231ef6f220d422b1839a6c441ffa1912c1c"), new Money(10000_00000000),"SQJvnHnxP2LhNJeP5uvPgacqrgH1nNnRuw"),
                new CollateralFederationMember(new PubKey("0300cda1f0d37683fc1441cdb8ed0f18190bc56c3f786116a127d3f03369f44b07"), new Money(10000_00000000),"SMs2EZssggQ5BcSuTmYgoXvhrNh1jJhHv2"),
                new CollateralFederationMember(new PubKey("0242c518c00b6890f14e0852cc039084fdca84fa5e9563b5d57ec150262b4dcb6c"), new Money(10000_00000000),"SX7YZNPNiD77pR9samtZszpgRQutyL7duH"),
                new CollateralFederationMember(new PubKey("02c0dec04c7ccc57c201b5f2e1db22bf4fce6c06be99dc7fec67190115208e835e"), new Money(10000_00000000),"SQzdzSufg7sQiFFHu9EG4YujaX9Jt8kE39"),
                new CollateralFederationMember(new PubKey("0204cc7a01d4423a83081b6711c1e93a38ec9ff115331da933ae59937d5c075ca3"), new Money(10000_00000000),"SPJjUkvrRo42w3qft7wtkuE5q4DCAxrmod"),
                new CollateralFederationMember(new PubKey("0317abe6a28cc7af44a46de97e7c6120c1ccec78afb83efe18030f5c36e3016b32"), new Money(10000_00000000),"SNKmwu9b5ABtUDASjk9QVpRyP7QQLzKPLd"),
                new CollateralFederationMember(new PubKey("03a75ed5b0cfe69957551d929492a5d7847b47c71de4a2c95c1036177c9294b9c3"), new Money(10000_00000000),"SSd2RbVC6nahmTQc7kaN9FUq2RCoEBkGuK"),
                new CollateralFederationMember(new PubKey("02b7af1d3e27ec3758bb59926ca3809013d6cd869808f4fae6d0426ce3166c6af2"), new Money(10000_00000000),"SZD9ipUwjuEuAwHgSquoreg1NCYs24y89a"),
                new CollateralFederationMember(new PubKey("03d621e270932fd41a29d9658384eb75bf00416b5b8351228f4653a06f4c942b68"), new Money(10000_00000000),"SeDpKWa1RJMoPyFEVYN5iyitAGTEJTPqqE"),
                new CollateralFederationMember(new PubKey("036a88ab8b860ecd00e6b35e3e04d353a2dd60937abc0a0d0e483220c1e95e51fc"), new Money(10000_00000000),"STxDmPYCxq3MEmtoYGk8oLRG1ujWe5FX3p"),
                new CollateralFederationMember(new PubKey("031eaad893aa056059c606ea9d4b2d2f21cdcb75ad1f4182dcc6d486ad2d3482c1"), new Money(10000_00000000),"Sj424EfSHG7WxRPxp2gBMfXqE3Wj6h3ZWz"),
                new CollateralFederationMember(new PubKey("025cb67811d0922ca77fa33f19c3e5c37961f9639a1f0a116011b9075f6796abcb"), new Money(10000_00000000),"ShMJHLrn9YVKPgZCnRu5fH8w6Pve18DL8Q"),
                new CollateralFederationMember(new PubKey("036437789fac0ab74cda93d98b519c28608a48ef86c3bd5e8227af606c1e025f61"), new Money(10000_00000000),"SNSwQVvB5FB6KPVT7325tJGWXbxVd4xceR"),
                new CollateralFederationMember(new PubKey("024ca136db3fd5f72e30ff91cbbdf9ab7a0a1da186b3fc7ad5f861a4742fa42cdd"), new Money(10000_00000000),"Sa6yravhxkUJVSjr6QjLiyUuH8YGZVZWzm"),
                new CollateralFederationMember(new PubKey("02a523078d5391f69ad3ee1554cf4afad3ce4c0946ff92c7447e5b7c7197967314"), new Money(10000_00000000),"SaZ8oZAasmSp5kJRnGx1aPDW5nqSjBxR7z"),
                new CollateralFederationMember(new PubKey("02d57eaa61845c5ce07963b211af83c3fe072a9de65c555f7bdbd7c38efe65e42a"), new Money(10000_00000000),"SSwiXJ8ENCcLyFDRxSnCaG9FS4UmtfaE5g"),
                new CollateralFederationMember(new PubKey("0371c8558c846172eaf694a4e3af4d6cfdbfdd0d8480666c206ea43522c65a926a"), new Money(10000_00000000),"SREEeESBB1fiSCEfZ7qDBuQeZtM7byCyoG"),
                new CollateralFederationMember(new PubKey("03adce7b60c2a3b03f9567d44bcf4e1d98200a736914a4385a4ef8c248d50b71ba"), new Money(10000_00000000),"ScY7ZaL5KN4PpHMaoAr3yK1qbRpEuq4rYv"),
                new CollateralFederationMember(new PubKey("028bbb6d3eca487640fab54c5800beb9e9d0f20c072805f08f0a4ae2af8bec596d"), new Money(10000_00000000),"SUGnHfLwuCidT3mRR6i8ZrNgYHPjBbdUzJ")

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
                EnforceMinProtocolVersionAtBlockHeight = 384675, // setting the value to zero makes the functionality inactive
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
                coinType: 401,
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
                maxMoney: Money.Coins(100_000_000),
                coinbaseMaturity: 1,
                premineHeight: 2,
                premineReward: Money.Coins(100_000_000),
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
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 28 }; // C
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 88 }; // c
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

                new DNSSeedData("cirrusmain1.stratisplatform.com", "cirrusmain1.stratisplatform.com")

            };

            this.StandardScriptsRegistry = new SmartContractsStandardScriptsRegistry();

            // 16 below should be changed to TargetSpacingSeconds when we move that field.
            Assert(this.DefaultBanTimeSeconds <= this.Consensus.MaxReorgLength * 16 / 2);
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("000005769503496300ec879afd7543dc9f86d3b3d679950b2b83e2f49f525856"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("1669a55d45b642af0ce82c5884cf5b8d8efd5bdcb9a450c95f442b9bd1ff65ea"));
        }
    }
}
