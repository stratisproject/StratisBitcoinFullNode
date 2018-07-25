
using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace NBitcoin.NetworkDefinitions
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
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0x21898467

            this.Name = "CityRegTest";
            this.Magic = magic;
            this.DefaultPort = 14333;
            this.RPCPort = 14334;
            this.MinTxFee = 0;
            this.FallbackFee = 0;
            this.MinRelayTxFee = 0;
            this.CoinTicker = "TCITY";

            // Taken from StratisMain Consensus options
            var consensus = new Consensus();
            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            consensus.BIP34Hash = new uint256("0x0000ee46643d31e70802b25996f2efc3229660c11d65fb70be19b49320ec8a9a");
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            consensus.LastPOWBlock = 12500;
            consensus.IsProofOfStake = true;
            consensus.ConsensusFactory = new PosConsensusFactory();
            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.CoinType = 4535;
            consensus.PremineReward = Money.Coins(13736000000);
            consensus.PremineHeight = 2;
            consensus.ProofOfWorkReward = Money.Coins(1); // Produced up until last POW block.
            consensus.ProofOfStakeReward = Money.Coins(100); // 52 560 000 a year.
            consensus.MaxReorgLength = 500;
            consensus.MaxMoney = long.MaxValue;

            // StratisRegTest differences
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            consensus.CoinbaseMaturity = 10;

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (66) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (66 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1428883200;
            this.GenesisNonce = 9945;
            this.GenesisBits = 0x1F00FFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2018-07-26: "We don’t need to fight the existing system, we just need to create a new one."
            // https://futurethinkers.org/vit-jedlicka-liberland/
            string pszTimestamp = "July 26, 2018, Future Thinkers, We don’t need to fight existing system, we create a new one";

            this.Consensus = consensus;

            this.Genesis = CreateCityGenesisBlock(consensus.ConsensusFactory, pszTimestamp, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            //this.Genesis.Header.Time = 1494909211;
            //this.Genesis.Header.Nonce = 2433759;
            //this.Genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = this.Genesis.GetHash();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0000ee46643d31e70802b25996f2efc3229660c11d65fb70be19b49320ec8a9a"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x0f874fd7797bbcf30f918ddde77ace58623f22f2118bf87f3fa84711471c250a"));
        }
    }
}