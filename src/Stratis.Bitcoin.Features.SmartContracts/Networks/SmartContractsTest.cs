using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.Features.SmartContracts.Networks
{
    public sealed class SmartContractsTest : Network
    {
        public SmartContractsTest()
        {
            this.Name = "SmartContractsTestNet";
            this.RootFolderName = StratisMain.StratisRootFolderName;
            this.DefaultConfigFilename = StratisMain.StratisDefaultConfigFilename;
            this.Magic = 0x0709110E; // Incremented 19/06
            this.DefaultPort = 18333;
            this.RPCPort = 18332;
            this.MaxTipAge = BitcoinMain.BitcoinDefaultMaxTipAgeInSeconds;
            this.MinTxFee = 1000;
            this.FallbackFee = 20000;
            this.MinRelayTxFee = 1000;

            var consensus = new NBitcoin.Consensus();
            consensus.ConsensusFactory = new SmartContractConsensusFactory();

            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 51;
            consensus.MajorityRejectBlockOutdated = 75;
            consensus.MajorityWindow = 100;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 21111;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 581885;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 330776;
            consensus.BIP34Hash = new uint256("0x0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8");
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")); // Set extremely low difficulty for now.
            consensus.MinimumChainWork = uint256.Zero;
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(20); // 20 second block time while on testnet 
            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = false;
            consensus.RuleChangeActivationThreshold = 1512; // 75% for testchains
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing

            consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
            consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1456790400, 1493596800);
            consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1462060800, 1493596800);

            consensus.CoinType = 1;

            consensus.DefaultAssumeValid = new uint256("0x000000003ccfe92231efee04df6621e7bb3f7f513588054e19f78d626b951f59"); // 1235126

            consensus.CoinbaseMaturity = 5;
            consensus.PremineHeight = 2;
            consensus.PremineReward = Money.Coins(1000000);
            consensus.ProofOfWorkReward = Money.Coins(50);
            consensus.ProofOfStakeReward = Money.Zero;
            consensus.MaxReorgLength = 500;
            consensus.MaxMoney = long.MaxValue;



            // Taken from StratisX.
            consensus.Options = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000
                );

            this.Genesis = BitcoinMain.CreateBitcoinGenesisBlock(consensus.ConsensusFactory, 1296688602, 414098458, 0x1d00ffff, 1, Money.Coins(50m));
            ((SmartContractBlockHeader)this.Genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");
            this.Genesis.Header.Nonce = 3; // Incremented 19/06
            consensus.HashGenesisBlock = this.Genesis.Header.GetHash();

            this.Consensus = consensus;

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            Bech32Encoder encoder = Encoders.Bech32("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();
        }
    }
}