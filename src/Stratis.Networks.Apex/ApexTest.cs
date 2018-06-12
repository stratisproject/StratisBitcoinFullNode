using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Networks.Apex
{
    public class ApexTest : ApexMain
    {
        public ApexTest()
        {
            this.Name = ApexNetwork.TestNetworkName;
            this.RootFolderName = ApexNetwork.ChainName.ToLowerInvariant();
            this.DefaultConfigFilename = $"{ApexNetwork.ChainName.ToLowerInvariant()}.conf";
            this.DefaultPort = 36001;
            this.RPCPort = 36101;
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 55 }; // P
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 117 }; // p
            this.Magic = 0x522357B;

            this.Consensus.CoinType = 3001;
            this.Consensus.DefaultAssumeValid = null;
            this.Consensus.PowLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));
            this.Consensus.CoinbaseMaturity = 10;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1528217189;
            this.GenesisNonce = 9027;
            this.GenesisBits = this.Consensus.PowLimit.ToCompact();
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            this.Genesis = ApexNetwork.CreateApexGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.Consensus.PowLimit, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == "0000e451752bac9f67cb5d63fd32442ba42d42b1b2ea28131d91e1e3f29f523b");
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("7326e99b152ce27951b0e9633c2663e8ddfd4006752d1721d579a22046e2d8fb"));
        }
    }
}