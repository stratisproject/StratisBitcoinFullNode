using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Networks
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
            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            consensus.LastPOWBlock = 12500;
            consensus.IsProofOfStake = true;
            consensus.ConsensusFactory = new PosConsensusFactory();
            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.CoinType = 105;
            consensus.PremineReward = Money.Coins(98000000);
            consensus.PremineHeight = 2;
            consensus.ProofOfWorkReward = Money.Coins(4);
            consensus.ProofOfStakeReward = Money.COIN;
            consensus.MaxReorgLength = 500;
            consensus.MaxMoney = long.MaxValue;

            // StratisRegTest differences
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            consensus.CoinbaseMaturity = 10;

            // Taken from StratisX.
            consensus.Options = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000
                );

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