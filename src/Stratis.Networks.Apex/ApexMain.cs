using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Networks;
using NBitcoin.Protocol;

namespace Stratis.Networks.Apex
{
    public class ApexMain : StratisMain
    {
        public ApexMain()
        {
            this.Name = ApexNetwork.MainNetworkName;
            this.RootFolderName = ApexNetwork.ChainName.ToLowerInvariant();
            this.DefaultConfigFilename = $"{ApexNetwork.ChainName.ToLowerInvariant()}.conf";
            this.DefaultPort = 36000;
            this.RPCPort = 36100;
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 23 }; // A
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 83 }; // a
            this.Magic = 0x522357A;

            this.Consensus.CoinType = 3000;
            this.Consensus.DefaultAssumeValid = null;
            this.Consensus.CoinbaseMaturity = 50;
            this.Consensus.PremineReward = Money.Coins(20000000);
            this.Consensus.ProofOfWorkReward = Money.Zero;
            this.Consensus.ProofOfStakeReward = Money.Zero;
            this.Consensus.MaxReorgLength = 0;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1528217223;
            this.GenesisNonce = 58285;
            this.GenesisBits = this.Consensus.PowLimit.ToCompact();
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            this.Genesis = ApexNetwork.CreateApexGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.Consensus.PowLimit, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == "000009a6434326a4851f0e95285351839c287182bd2b62ca8765ce30007605e1");
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("070b7da316f93439d05240db30a9ca4f6019d550e0b6af9a8ac1b075726c9403"));
        }
    }
}