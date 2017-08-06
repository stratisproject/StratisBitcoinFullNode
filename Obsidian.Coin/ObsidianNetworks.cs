using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Obsidian.Coin
{
	public static class ObsidianNetworks
	{
		public static Network CreateMainnet()
		{
			Block.BlockSignature = true; // ?
			Transaction.TimeStamp = true; // ?
			NetConfig.UseSha512OnMain = false;

			NetworkBuilder builder = new NetworkBuilder();

			Block mainGenesisBlock = GenesisBlock.CreateMainGenesisBlock();
			
			var odnMainConsensus = new Consensus
			{
				SubsidyHalvingInterval = 210000,
				MajorityEnforceBlockUpgrade = 750,
				MajorityRejectBlockOutdated = 950,
				MajorityWindow = 1000,
				BIP34Hash = null,
				PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
				PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks, 20160 minutes
				PowTargetSpacing = TimeSpan.FromSeconds(10 * 60), // 10 minutes
				PowAllowMinDifficultyBlocks = false,
				PowNoRetargeting = false,
				RuleChangeActivationThreshold = 1916, // 95% of 2016
				MinerConfirmationWindow = 2016, // nPowTargetTimespan / nPowTargetSpacing
				CoinbaseMaturity = 100,
				HashGenesisBlock = mainGenesisBlock.GetHash(),
				GetPoWHash = ObsidianPoWHash.GetPoWHash,
				LitecoinWorkCalculation = false,
				// PoS
				LastPOWBlock = 12500,
				ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
				ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false))
		};

			odnMainConsensus.BIP34Hash = odnMainConsensus.HashGenesisBlock;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP34] = 227931;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP65] = 388381;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP66] = 363725;
			odnMainConsensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
			odnMainConsensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800);
			odnMainConsensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, 0);

			// Start copied from StratisMain
			var pchMessageStart = new byte[4];
			pchMessageStart[0] = 0x70;
			pchMessageStart[1] = 0x35;
			pchMessageStart[2] = 0x22;
			pchMessageStart[3] = 0x05;
			var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570; 
			// End copied from StratisMain

			_mainnet = builder.SetConsensus(odnMainConsensus)
				// Start copied from StratisMain
				.SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (63) })
				.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (125) })
				.SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (63 + 128) })
				.SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
				.SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
				.SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
				.SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })
				.SetBase58Bytes(Base58Type.PASSPHRASE_CODE, new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 })
				.SetBase58Bytes(Base58Type.CONFIRMATION_CODE, new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A })
				.SetBase58Bytes(Base58Type.STEALTH_ADDRESS, new byte[] { 0x2a })
				.SetBase58Bytes(Base58Type.ASSET_ID, new byte[] { 23 })
				.SetBase58Bytes(Base58Type.COLORED_ADDRESS, new byte[] { 0x13 })
				.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "bc")
				.SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, "bc") // bc?
				// End copied from StratisMain
				.SetMagic(magic)
				.SetPort(NetConfig.MainnetPort)
				.SetRPCPort(NetConfig.MainnetRpcPort)
				.SetName("odn-main")
				.AddAlias("odn-mainnet")
				.AddAlias("obsidian-main")
				.AddAlias("obsidian-mainnet")
				.AddDNSSeeds(new DNSSeedData[]
				{
					//new DNSSeedData("obsidianseednode1.westeurope.cloudapp.azure.com", "obsidianseednode1.westeurope.cloudapp.azure.com")
				})
				.AddSeeds(ToSeed(pnSeed6_main))
				.SetGenesis(mainGenesisBlock)
				.BuildAndRegister();
			return _mainnet;
		}

		public static Network CreateTestnet()
		{
			// atm, this supposed to be strictly Stratis Testnet!

			Block.BlockSignature = true;
			Transaction.TimeStamp = true;

			var consensus = Network.StratisMain.Consensus.Clone();
			consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

			var pchMessageStart = new byte[4];
			pchMessageStart[0] = 0x71;
			pchMessageStart[1] = 0x31;
			pchMessageStart[2] = 0x21;
			pchMessageStart[3] = 0x11;
			var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570; 

			var genesis = Network.StratisMain.GetGenesis().Clone();
			genesis.Header.Time = 1493909211;
			genesis.Header.Nonce = 2433759;
			genesis.Header.Bits = consensus.PowLimit;
			consensus.HashGenesisBlock = genesis.GetHash();

			//assert(consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));

			var builder = new NetworkBuilder()
				.SetName("StratisTest")
				.SetConsensus(consensus)
				.SetMagic(magic)
				.SetGenesis(genesis)
				.SetPort(26178)
				.SetRPCPort(26174)
				.SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
				.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
				.SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
				.SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
				.SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
				.SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
				.SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })


				.AddDNSSeeds(new DNSSeedData[]
				{

				});

			return builder.BuildAndRegister();
		}

		static Tuple<byte[], int>[] pnSeed6_main = {

		};
		static Tuple<byte[], int>[] pnSeed6_test = {

		};

		static IEnumerable<NetworkAddress> ToSeed(Tuple<byte[], int>[] tuples)
		{
			return tuples
				.Select(t => new NetworkAddress(new IPAddress(t.Item1), t.Item2))
				.ToArray();
		}

		static Network _mainnet;
		public static Network Mainnet
		{
			get
			{
				AssertRegistered();
				return _mainnet;
			}
		}

		static void AssertRegistered()
		{
			if (_mainnet == null)
				throw new InvalidOperationException("You need to call LitecoinNetworks.Register() before using the litecoin networks");
		}

		static Network _testnet;
		public static Network Testnet
		{
			get
			{
				AssertRegistered();
				return _testnet;
			}
		}
	}
}
