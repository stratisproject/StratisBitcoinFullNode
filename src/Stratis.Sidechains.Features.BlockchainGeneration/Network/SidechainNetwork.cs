using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Networks;
using NBitcoin.Protocol;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public class SidechainNetwork
    {
        public const string SidechainMainName = "SidechainMain";
        public const string SidechainTestName = "SidechainTest";
        public const string SidechainRegTestName = "SidechainRegTest";

        public static Network SidechainMain => Network.GetNetwork(SidechainMainName) ?? Network.Register(new SidechainMain());

        public static Network SidechainTest => Network.GetNetwork(SidechainTestName) ?? Network.Register(new SidechainTest());

        public static Network SidechainRegTest => Network.GetNetwork(SidechainRegTestName) ?? Network.Register(new SidechainRegTest());

        public static Block CreateSidechainGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
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

    public class SidechainMain : StratisMain
    {
        public SidechainMain()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x70;
            messageStart[1] = 0x35;
            messageStart[2] = 0x22;
            messageStart[3] = 0x05;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 


            SidechainInfo sidechainInfo = SidechainIdentifier.Instance.InfoProvider
                .GetSidechainInfo(SidechainIdentifier.Instance.Name);
            var networkInfo = sidechainInfo.MainNet;

            this.Name = SidechainNetwork.SidechainMainName;
            this.RootFolderName = SidechainIdentifier.Instance.Name;
            this.DefaultConfigFilename = $"{SidechainIdentifier.Instance.Name}.conf";
            this.DefaultPort = networkInfo.Port;
            this.RPCPort = networkInfo.RpcPort;
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (byte)networkInfo.AddressPrefix };
            this.Magic = 0;

            this.Consensus.CoinType = sidechainInfo.CoinType;
            this.Consensus.DefaultAssumeValid = null;

            this.Genesis = SidechainNetwork.CreateSidechainGenesisBlock(this.Consensus.ConsensusFactory, networkInfo.Time, networkInfo.Nonce, this.Consensus.PowLimit, 1, Money.Coins(50m));
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);
        }
    }

    public class SidechainTest : SidechainMain
    {
        public SidechainTest()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x31;
            messageStart[2] = 0x21;
            messageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 

            SidechainInfo sidechainInfo = SidechainIdentifier.Instance.InfoProvider
                .GetSidechainInfo(SidechainIdentifier.Instance.Name);
            var networkInfo = sidechainInfo.TestNet;

            this.Name = SidechainNetwork.SidechainMainName;
            this.RootFolderName = SidechainIdentifier.Instance.Name;
            this.DefaultConfigFilename = $"{SidechainIdentifier.Instance.Name}.conf";
            this.DefaultPort = networkInfo.Port; //36178 updated for sidechains
            this.RPCPort = networkInfo.RpcPort;  //36174 updated for sidechains
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (byte)networkInfo.AddressPrefix }; //65     //updated for sidechains
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };
            this.Magic = magic;

            this.Consensus.CoinType = sidechainInfo.CoinType;
            this.Consensus.DefaultAssumeValid = null;
            this.Consensus.PowLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));

            this.Genesis = SidechainNetwork.CreateSidechainGenesisBlock(this.Consensus.ConsensusFactory, networkInfo.Time, networkInfo.Nonce, this.Consensus.PowLimit, 1, Money.Coins(50m));
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);
        }
    }

    public class SidechainRegTest : SidechainMain
    {
        public SidechainRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0xcd;
            messageStart[1] = 0xf2;
            messageStart[2] = 0xc0;
            messageStart[3] = 0xef;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            var networkInfo = SidechainIdentifier.Instance.InfoProvider
                .GetSidechainInfo(SidechainIdentifier.Instance.Name).RegTest;

            this.Name = SidechainNetwork.SidechainRegTestName;
            this.RootFolderName = SidechainIdentifier.Instance.Name;
            this.DefaultConfigFilename = $"{SidechainIdentifier.Instance.Name}.conf";
            this.DefaultPort = networkInfo.Port;
            this.RPCPort = networkInfo.RpcPort;

            this.Magic = magic;
            this.MinTxFee = 0;
            this.FallbackFee = 0;
            this.MinRelayTxFee = 0;

            this.Consensus.PowAllowMinDifficultyBlocks = true;
            this.Consensus.PowNoRetargeting = true;
            this.Consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (byte)networkInfo.AddressPrefix };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            this.Genesis = SidechainNetwork.CreateSidechainGenesisBlock(this.Consensus.ConsensusFactory, networkInfo.Time, networkInfo.Nonce, this.Consensus.PowLimit, 1, Money.Coins(50m));
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash();
            Assert(this.Consensus.HashGenesisBlock.ToString() == networkInfo.GenesisHashHex);
        }
    }
}
