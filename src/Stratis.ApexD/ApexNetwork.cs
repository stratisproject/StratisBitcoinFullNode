using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Networks;
using NBitcoin.Protocol;

namespace Stratis.ApexD
{
    public class ApexNetwork
    {
        public const string ChainName = "Apex";

        public const string MainNetworkName = "ApexMain";
        public const string TestNetworkName = "ApexTest";
        public const string RegTestNetworkName = "ApexRegTest";

        public static Network ApexMain => Network.GetNetwork(MainNetworkName) ?? Network.Register(new ApexMain());

        public static Network ApexTest => Network.GetNetwork(TestNetworkName) ?? Network.Register(new ApexTest());

        public static Network ApexRegTest => Network.GetNetwork(RegTestNetworkName) ?? Network.Register(new ApexRegTest());

        public static Block CreateApexGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });
            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }

        private static void Assert(bool condition)
        {
            // TODO: use Guard when this moves to the FN.
            if (!condition)
            {
                throw new InvalidOperationException("Invalid network");
            }
        }
    }

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

            this.Consensus.CoinType = 3001;
            this.Consensus.PowAllowMinDifficultyBlocks = true;
            this.Consensus.PowNoRetargeting = true;
            this.Consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

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

            this.Genesis = ApexNetwork.CreateApexGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.Consensus.PowLimit, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == "0688f8440feed473792edc56e365bb7ab20ae2d4e2010cfb8af4ccadaa53e611");
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("a835d145c6011d3731b0ab5a8f2108f635a5197ccd2e69b74cac1dfa9007db4b"));
        }
    }
}
