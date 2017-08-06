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
		public static Network RegisterMain()
		{
			Block.BlockSignature = true; // ?
			Transaction.TimeStamp = true; // ?
			NetConfig.UseSha512OnMain = true;

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

		public static Network RegisteTest()
		{
			NetworkBuilder builder = new NetworkBuilder();


			var genesis = GenesisBlock.CreateMainGenesisBlock();

			// Mainnet
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
				HashGenesisBlock = new uint256("12a765e31ffd4059bada1e25190f6e98c99d9714d334efa41a195a7e7e04bfe2"),
				GetPoWHash = ObsidianPoWHash.GetPoWHash,
				LitecoinWorkCalculation = false,
				// PoS
				LastPOWBlock = 12500,
				ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
				ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false))
			};

			odnMainConsensus.BIP34Hash = odnMainConsensus.HashGenesisBlock;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
			odnMainConsensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
			odnMainConsensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
			odnMainConsensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800);
			odnMainConsensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, 0);



			_mainnet = builder.SetConsensus(odnMainConsensus)
				.SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { 48 })
				.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { 50 })
				.SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { 176 })
				.SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { 0x04, 0x88, 0xB2, 0x1E })
				.SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { 0x04, 0x88, 0xAD, 0xE4 })
				.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, Encoders.Bech32("odn"))
				.SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, Encoders.Bech32("odn"))
				.SetMagic(0xdbb6c0fb)
				.SetPort(NetConfig.MainnetPort)
				.SetRPCPort(NetConfig.TestnetPort)
				.SetName("odn-main")
				.AddAlias("odn-mainnet")
				.AddAlias("obsidian-main")
				.AddAlias("obsidian-mainnet")
				.AddDNSSeeds(new[]
				{
					new DNSSeedData("obsidianseednode1.westeurope.cloudapp.azure.com", "obsidianseednode1.westeurope.cloudapp.azure.com")
				})
				.AddSeeds(ToSeed(pnSeed6_main))
				.SetGenesis(genesis)
				.BuildAndRegister();
			return Mainnet;
			// Testnet
			builder = new NetworkBuilder();

			_testnet = builder.SetConsensus(new Consensus()
				{
					SubsidyHalvingInterval = 840000,
					MajorityEnforceBlockUpgrade = 51,
					MajorityRejectBlockOutdated = 75,
					MajorityWindow = 1000,
					PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
					PowTargetTimespan = TimeSpan.FromSeconds(3.5 * 24 * 60 * 60),
					PowTargetSpacing = TimeSpan.FromSeconds(2.5 * 60),
					PowAllowMinDifficultyBlocks = true,
					PowNoRetargeting = false,
					RuleChangeActivationThreshold = 1512,
					MinerConfirmationWindow = 2016,
					CoinbaseMaturity = 100,
					HashGenesisBlock = new uint256("f5ae71e26c74beacc88382716aced69cddf3dffff24f384e1808905e0188f68f"),
					GetPoWHash = ObsidianPoWHash.GetPoWHash,
					LitecoinWorkCalculation = true
				})
				.SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { 111 })
				.SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { 58 })
				.SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { 239 })
				.SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { 0x04, 0x35, 0x87, 0xCF })
				.SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { 0x04, 0x35, 0x83, 0x94 })
				.SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, Encoders.Bech32("todn"))
				.SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, Encoders.Bech32("todn"))
				.SetMagic(0xf1c8d2fd)
				.SetPort(NetConfig.TestnetPort)
				.SetRPCPort(NetConfig.TestnetRpcPort)
				.SetName("odn-test")
				.AddAlias("odn-testnet")
				.AddAlias("obsidian-test")
				.AddAlias("obsidian-testnet")
				.AddDNSSeeds(new[]
				{
					new DNSSeedData("obsidianseednode1.westeurope.cloudapp.azure.com", "obsidianseednode1.westeurope.cloudapp.azure.com")
				})
				.AddSeeds(ToSeed(pnSeed6_test))
				.SetGenesis(new Block(Encoders.Hex.DecodeData("010000000000000000000000000000000000000000000000000000000000000000000000d9ced4ed1130f7b7faad9be25323ffafa33232a17c3edf6cfd97bee6bafbdd97f6028c4ef0ff0f1e38c3f6160101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff4804ffff001d0104404e592054696d65732030352f4f63742f32303131205374657665204a6f62732c204170706c65e280997320566973696f6e6172792c2044696573206174203536ffffffff0100f2052a010000004341040184710fa689ad5023690c80f3a49c8f13f8d45b8c857fbcbc8bc4a8e4d3eb4b10f4d4604fa08dce601aaf0f470216fe1b51850b4acf21b179c45070ac7b03a9ac00000000")))
				.BuildAndRegister();
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
