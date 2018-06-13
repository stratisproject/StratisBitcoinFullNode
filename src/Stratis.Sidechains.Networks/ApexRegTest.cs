using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Sidechains.Networks
{
    public class ApexRegTest : ApexMain
    {
        public ApexRegTest()
        {
            this.Name = ApexNetwork.RegTestNetworkName;
            this.RootFolderName = ApexNetwork.ChainName.ToLowerInvariant();
            this.DefaultConfigFilename = $"{ApexNetwork.ChainName.ToLowerInvariant()}.conf";
            this.DefaultPort = 36002;
            this.RPCPort = 36102;
            this.Magic = 0x522357C;
            this.MinTxFee = 0;
            this.FallbackFee = 0;
            this.MinRelayTxFee = 0;
            this.CoinTicker = "TAPX";

            this.Consensus.CoinType = 3001;
            this.Consensus.PowAllowMinDifficultyBlocks = true;
            this.Consensus.PowNoRetargeting = true;
            this.Consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.
            this.Consensus.CoinbaseMaturity = 10;
            this.Consensus.MaxMoney = Money.Coins(20000000);

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 75 }; // X
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 137 }; // x

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            // Create the genesis block.
            this.GenesisTime = 1528217336;
            this.GenesisNonce = 2;
            this.GenesisBits = this.Consensus.PowLimit.ToCompact();
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            this.Genesis = ApexNetwork.CreateGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.Consensus.PowLimit, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == "0688f8440feed473792edc56e365bb7ab20ae2d4e2010cfb8af4ccadaa53e611");
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("a835d145c6011d3731b0ab5a8f2108f635a5197ccd2e69b74cac1dfa9007db4b"));
        }
    }
}