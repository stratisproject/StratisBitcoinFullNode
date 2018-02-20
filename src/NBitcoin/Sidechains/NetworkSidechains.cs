using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public partial class Network
    {
        public static Network SidechainMain => Network.GetNetwork("SidechainMain") ?? InitSidechainMain();

        public static Network SidechainTestNet => Network.GetNetwork("SidechainTestNet") ?? InitSidechainTest();

        public static Network SidechainRegTest => Network.GetNetwork("SidechainRegTest") ?? InitSidechainRegTest();
    }

    public partial class Network
	{
		private static Network InitSidechainMain()
		{
            //not yet supported
            throw new NotSupportedException();
		}

		private static Network InitSidechainTest()
		{
		    var networkInfo = SidechainIdentifier.Instance.InfoProvider
                .GetSidechainInfo(SidechainIdentifier.Instance.Name).TestNet;

            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = Network.StratisMain.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x31;
            messageStart[2] = 0x21;
            messageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 

            var genesis = Network.StratisMain.GetGenesis().Clone();
		    genesis.Header.Time = networkInfo.Time;              //1510160966;   //updated for sidechains
		    genesis.Header.Nonce = networkInfo.Nonce;            // 2433759;     //updated for sidechains
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            ////uint256.Parse("5bdec714f54f673d133a8aab62478708bae6ad99e096455cec4d7864ccda6f8c")
            Assert(consensus.HashGenesisBlock == networkInfo.GenesisHash );

            consensus.DefaultAssumeValid = null; // turn off assumevalid for sidechains.

            var builder = new NetworkBuilder()
		        .SetName("SidechainTestNet")
                .SetRootFolderName(SidechainIdentifier.Instance.Name)
                .SetConsensus(consensus)
		        .SetMagic(magic)
		        .SetGenesis(genesis)
		        .SetPort(networkInfo.Port)              //36178 updated for sidechains
                .SetRPCPort(networkInfo.RpcPort)        //36174 updated for sidechains
                .SetTxFees(10000, 60000, 10000)
		        .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] {( (byte) networkInfo.AddressPrefix)})              //65     //updated for sidechains
		        .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] {(196)})
		        .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] {(65 + 128)})
		        .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] {0x01, 0x42})
		        .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] {0x01, 0x43})
		        .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] {(0x04), (0x88), (0xB2), (0x1E)})
		        .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] {(0x04), (0x88), (0xAD), (0xE4)});

                //updated for sidechains - no seeds
                //.AddDNSSeeds(new[]
                //{
                //    new DNSSeedData("testnode1.stratisplatform.com", "testnode1.stratisplatform.com"),
                //     new DNSSeedData("testnode2.stratis.cloud", "testnode2.stratis.cloud"),
                //   new DNSSeedData("testnode3.stratisplatform.com", "testnode3.stratisplatform.com")
                //});

                //builder.AddSeeds(new[] { new NetworkAddress(IPAddress.Parse("51.141.28.47"), builder.Port) }); // the c# testnet node

            return builder.BuildAndRegister();
		}

		private static Network InitSidechainRegTest()
		{ 
            //not yet supported
		    throw new NotSupportedException();
        }

	    internal static Block CreateSidechainGenesisBlock(uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
		{
			string pszTimestamp = "http://www.theonion.com/article/olympics-head-priestess-slits-throat-official-rio--53466";
			return CreateSidechainGenesisBlock(pszTimestamp, nTime, nNonce, nBits, nVersion, genesisReward);
		}

		private static Block CreateSidechainGenesisBlock(string pszTimestamp, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
		{
			Transaction txNew = new Transaction();
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
			Block genesis = new Block();
			genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
			genesis.Header.Bits = nBits;
			genesis.Header.Nonce = nNonce;
			genesis.Header.Version = nVersion;
			genesis.Transactions.Add(txNew);
			genesis.Header.HashPrevBlock = uint256.Zero;
			genesis.UpdateMerkleRoot();
			return genesis;
		}
	}
}