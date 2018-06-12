using System.Collections.Generic;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin.Networks
{
    public class BitcoinRegTest : BitcoinMain
    {
        public BitcoinRegTest()
        {
            this.Name = "RegTest";
            this.AdditionalNames = new List<string> {"reg"};
            this.Consensus.CoinType = 0;
            this.Magic = 0xDAB5BFFA;
            this.DefaultPort = 18444;
            this.RPCPort = 18332;
            this.CoinTicker = "TBTC";

            this.Consensus.PowAllowMinDifficultyBlocks = true;
            this.Consensus.PowNoRetargeting = true;
            this.Consensus.RuleChangeActivationThreshold = 108;
            this.Consensus.MinerConfirmationWindow = 144;
            this.Consensus.SubsidyHalvingInterval = 150;
            this.Consensus.BIP34Hash = new uint256();
            this.Consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.MinimumChainWork = uint256.Zero;
            this.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 100000000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 100000000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 100000000;
            this.Consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 0, 999999999);
            this.Consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 0, 999999999);
            this.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999);

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

            this.Genesis = CreateBitcoinGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"));
        }
    }
}
