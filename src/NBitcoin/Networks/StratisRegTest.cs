
using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace NBitcoin.Networks
{
    public class StratisRegTest : StratisMain
    {
        public StratisRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0xcd;
            messageStart[1] = 0xf2;
            messageStart[2] = 0xc0;
            messageStart[3] = 0xef;
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            this.Name = "StratisRegTest";
            this.Magic = magic;
            this.DefaultPort = 18444;
            this.RPCPort = 18442;
            this.MinTxFee = 0;
            this.FallbackFee = 0;
            this.MinRelayTxFee = 0;
            this.CoinTicker = "TSTRAT";

            // Taken from StratisMain Consensus options
            var consensus = new Consensus();
            consensus.SubsidyHalvingInterval = StratisMain.Consensus.SubsidyHalvingInterval;
            consensus.MajorityEnforceBlockUpgrade = StratisMain.Consensus.MajorityEnforceBlockUpgrade;
            consensus.MajorityRejectBlockOutdated = StratisMain.Consensus.MajorityRejectBlockOutdated;
            consensus.MajorityWindow = StratisMain.Consensus.MajorityWindow;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = StratisMain.Consensus.BuriedDeployments[BuriedDeployments.BIP34];
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = StratisMain.Consensus.BuriedDeployments[BuriedDeployments.BIP65];
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = StratisMain.Consensus.BuriedDeployments[BuriedDeployments.BIP66];
            consensus.BIP34Hash = StratisMain.Consensus.BIP34Hash;
            consensus.PowTargetTimespan = StratisMain.Consensus.PowTargetTimespan;
            consensus.PowTargetSpacing = StratisMain.Consensus.PowTargetSpacing;
            consensus.RuleChangeActivationThreshold = StratisMain.Consensus.RuleChangeActivationThreshold; // 95% of 2016
            consensus.MinerConfirmationWindow = StratisMain.Consensus.MinerConfirmationWindow; // nPowTargetTimespan / nPowTargetSpacing
            consensus.LastPOWBlock = StratisMain.Consensus.LastPOWBlock;
            consensus.IsProofOfStake = StratisMain.Consensus.IsProofOfStake;
            consensus.ConsensusFactory = new PosConsensusFactory() { Consensus = consensus };
            consensus.ProofOfStakeLimit = new BigInteger(StratisMain.Consensus.ProofOfStakeLimit.ToByteArray());
            consensus.ProofOfStakeLimitV2 = new BigInteger(StratisMain.Consensus.ProofOfStakeLimitV2.ToByteArray());
            consensus.CoinType = StratisMain.Consensus.CoinType;
            consensus.PremineReward = new Money(StratisMain.Consensus.PremineReward);
            consensus.PremineHeight = StratisMain.Consensus.PremineHeight;
            consensus.ProofOfWorkReward = new Money(StratisMain.Consensus.ProofOfWorkReward);
            consensus.ProofOfStakeReward = new Money(StratisMain.Consensus.ProofOfStakeReward);
            consensus.MaxReorgLength = StratisMain.Consensus.MaxReorgLength;
            consensus.MaxMoney = StratisMain.Consensus.MaxMoney;

            // StratisRegTest differences
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            consensus.CoinbaseMaturity = 10;

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1470467000;
            this.GenesisNonce = 1831645;
            this.GenesisBits = 0x1e0fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            this.Genesis = CreateStratisGenesisBlock(consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            this.Genesis.Header.Time = 1494909211;
            this.Genesis.Header.Nonce = 2433759;
            this.Genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = this.Genesis.GetHash();

            this.Consensus = consensus;

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"));
        }
    }
}
