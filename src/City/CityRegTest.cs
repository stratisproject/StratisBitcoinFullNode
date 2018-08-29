using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace City.Networks
{
    public class CityRegTest : CityMain
    {
        public CityRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x67;
            messageStart[1] = 0x84;
            messageStart[2] = 0x89;
            messageStart[3] = 0x21;
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            this.Name = "CityRegTest";
            this.Magic = magic;
            this.DefaultPort = 14333;
            this.RPCPort = 14334;
            this.MinTxFee = 0;
            this.FallbackFee = 0;
            this.MinRelayTxFee = 0;
            this.CoinTicker = "TCITY";

            var powLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1428883200;
            this.GenesisNonce = 9945;
            this.GenesisBits = 0x1F00FFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2018-07-26: "We don’t need to fight the existing system, we just need to create a new one."
            // https://futurethinkers.org/vit-jedlicka-liberland/
            string pszTimestamp = "July 26, 2018, Future Thinkers, We don’t need to fight existing system, we create a new one";

            Block genesisBlock = CreateCityGenesisBlock(pszTimestamp, consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            //genesisBlock.Header.Time = 1494909211;
            //genesisBlock.Header.Nonce = 2433759;
            //genesisBlock.Header.Bits = powLimit;

            this.Genesis = genesisBlock;

            // Taken from StratisX.
            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000
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
                coinType: 4535,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x0000ee46643d31e70802b25996f2efc3229660c11d65fb70be19b49320ec8a9a"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                defaultAssumeValid: null, // turn off assumevalid for regtest.
                maxMoney: long.MaxValue,
                coinbaseMaturity: 10,
                premineHeight: 2,
                premineReward: Money.Coins(13736000000),
                proofOfWorkReward: Money.Coins(1),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: true,
                powNoRetargeting: true,
                powLimit: powLimit,
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 125000,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(100) // 52 560 000 a year.
            );

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (66) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (66 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0000ee46643d31e70802b25996f2efc3229660c11d65fb70be19b49320ec8a9a"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x0f874fd7797bbcf30f918ddde77ace58623f22f2118bf87f3fa84711471c250a"));
        }
    }
}