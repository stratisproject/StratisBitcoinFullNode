﻿using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Networks
{
    public class StratisTest : StratisMain
    {
        public StratisTest()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x31;
            messageStart[2] = 0x21;
            messageStart[3] = 0x11;
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0x11213171;

            this.Name = "StratisTest";
            this.Magic = magic;
            this.DefaultPort = 26178;
            this.RPCPort = 26174;
            this.CoinTicker = "TSTRAT";

            var powLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1470467000;
            this.GenesisNonce = 1831645;
            this.GenesisBits = 0x1e0fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            Block genesisBlock = CreateStratisGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            
            genesisBlock.Header.Time = 1493909211;
            genesisBlock.Header.Nonce = 2433759;
            genesisBlock.Header.Bits = powLimit;

            this.Genesis = genesisBlock;

            // Taken from StratisX.
            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                provenHeadersActivationHeight: 10_000_000 // TODO: Set it to the real value once it is known.
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new BIP9DeploymentsArray();

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 105,
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
                maxReorgLength: 500,
                defaultAssumeValid: new uint256("0x98fa6ef0bca5b431f15fd79dc6f879dc45b83ed4b1bbe933a383ef438321958e"), // 372652
                maxMoney: long.MaxValue,
                coinbaseMaturity: 10,
                premineHeight: 2,
                premineReward: Money.Coins(98000000),
                proofOfWorkReward: Money.Coins(4),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                powNoRetargeting: false,
                powLimit: powLimit,
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 12500,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.COIN
            );

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
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

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("testnet1.stratisplatform.com", "testnet1.stratisplatform.com"),
                new DNSSeedData("testnet2.stratisplatform.com", "testnet2.stratisplatform.com"),
                new DNSSeedData("testnet3.stratisplatform.com", "testnet3.stratisplatform.com"),
                new DNSSeedData("testnet4.stratisplatform.com", "testnet4.stratisplatform.com")
            };

            this.SeedNodes = new List<NetworkAddress>
            {
                new NetworkAddress(IPAddress.Parse("51.140.231.125"), this.DefaultPort), // danger cloud node
                new NetworkAddress(IPAddress.Parse("13.70.81.5"), 3389), // beard cloud node  
                new NetworkAddress(IPAddress.Parse("191.235.85.131"), 3389), // fassa cloud node  
                new NetworkAddress(IPAddress.Parse("52.232.58.52"), 26178), // neurosploit public node
            };

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));
        }
    }
}