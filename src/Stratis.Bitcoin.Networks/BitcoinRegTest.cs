using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Networks
{
    public class BitcoinRegTest : BitcoinMain
    {
        public BitcoinRegTest()
        {
            this.Name = "RegTest";
            this.AdditionalNames = new List<string> {"reg"};
            this.Magic = 0xDAB5BFFA;
            this.DefaultPort = 18444;
            this.RPCPort = 18332;
            this.CoinTicker = "TBTC";

            // Taken from BitcoinMain Consensus options
            var consensus = new Consensus();
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.CoinbaseMaturity = 100;
            consensus.PremineReward = Money.Zero;
            consensus.ProofOfWorkReward = Money.Coins(50);
            consensus.ProofOfStakeReward = Money.Zero;
            consensus.MaxReorgLength = 0;
            consensus.MaxMoney = 21000000 * Money.COIN;

            // BitcoinRegTest differences
            consensus.CoinType = 0;
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;
            consensus.RuleChangeActivationThreshold = 108;
            consensus.MinerConfirmationWindow = 144;
            consensus.SubsidyHalvingInterval = 150;
            consensus.BIP34Hash = new uint256();
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.MinimumChainWork = uint256.Zero;
            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 100000000;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 100000000;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 100000000;
            consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 0, 999999999);
            consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 0, 999999999);
            consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999);

            consensus.Options = new ConsensusOptions(); // Default - set to Bitcoin params.

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };

            var encoder = new Bech32Encoder("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();
            
            // Create the genesis block.
            this.GenesisTime = 1296688602;
            this.GenesisNonce = 2;
            this.GenesisBits = 0x207fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            this.Genesis = CreateBitcoinGenesisBlock(consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            consensus.HashGenesisBlock = this.Genesis.GetHash();

            this.Consensus = consensus;

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"));
        }
    }
}