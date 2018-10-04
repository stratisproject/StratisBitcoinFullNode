using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace City.Networks
{
    public class CityTest : CityMain
    {
        public CityTest()
        {
            this.Name = "CityTest";
            this.Magic = 0x43545401; // .CTT
            this.DefaultPort = 24333;
            this.RPCPort = 24334;
            this.CoinTicker = "TCITY";

            var powLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1538395200; // 10/01/2018 @ 12:00pm (UTC)
            this.GenesisNonce = 6967;
            this.GenesisBits = 0x1F0FFFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2017-08-18: "Libertarianisme er en politisk tankeretning som legger stor vekt på individuell frihet, og prosjektet er blitt omtalt over hele verden på sentrale nettsider for folk som handler med kryptovalutaer, altså penger og sikre verdipapirer som fungerer helt uten en sentral myndighet."
            // https://morgenbladet.no/aktuelt/2017/08/privatlivets-fred-i-liberstad
            string pszTimestamp = "August 18 2017, Morgenbladet, Money that work without a central authority";

            Block genesisBlock = CreateCityGenesisBlock(pszTimestamp, consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            
            //genesisBlock.Header.Time = 1493909211;
            //genesisBlock.Header.Nonce = 2433759;
            //genesisBlock.Header.Bits = powLimit;

            this.Genesis = genesisBlock;

            // Taken from StratisX.
            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                provenHeadersActivationHeight: 20_000_000 // TODO: Set it to the real value once it is known.
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
                coinType: 1926,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                defaultAssumeValid: new uint256("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"),
                maxMoney: long.MaxValue,
                coinbaseMaturity: 10,
                premineHeight: 2,
                premineReward: Money.Coins(13736000000),
                proofOfWorkReward: Money.Coins(2),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                powNoRetargeting: false,
                powLimit: powLimit,
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 125000,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(20)
            );

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (66) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (66 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0xcf917ba726c8d05496a6b144fc433dc06cc574f49ca429e250454a0bbaab926d"), new uint256("0x315e64b6097a15128b0379c501ef278ff4fa70b062b44ce69a95e604464c46f8")) }, // Premine
                { 50, new CheckpointInfo(new uint256("0x464ae37e22cc44d0c8a86478ff95f98a6e1c44ceb3e175181e1a382270d1780c"), new uint256("0x6b5e010988dc9716e010e3ec21f7f60a4850726d5bbc7c212fab4e7b9b3566d7")) },
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("city-chain.org", "testseed.city-chain.org"),
                new DNSSeedData("city-coin.org", "testseed.city-coin.org"),
                new DNSSeedData("citychain.foundation", "testseed.citychain.foundation")
            };

            this.SeedNodes = new List<NetworkAddress>
            {
                new NetworkAddress(IPAddress.Parse("40.115.2.6"), this.DefaultPort),
                new NetworkAddress(IPAddress.Parse("13.66.158.6"), this.DefaultPort),
                new NetworkAddress(IPAddress.Parse("52.175.194.227"), this.DefaultPort)
            };

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x00077765f625cc2cb6266544ff7d5a462f25be14ea1116dc2bd2fec17e40a5e3"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x034aaae9ca8e297078a2fed80cdaaf72ffad8aa1b1988c7b6edd8f01d69312ca"));
        }
    }
}