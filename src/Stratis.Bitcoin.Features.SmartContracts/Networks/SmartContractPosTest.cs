using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.Features.SmartContracts.Networks
{
    public class SmartContractPosTest : Network
    {
        public SmartContractPosTest()
        {
            this.Name = "SmartContractsPosTestNet";
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
            consensus.ConsensusFactory = new SmartContractPosConsensusFactory();
            consensus.IsProofOfStake = true;

            consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            consensus.CoinbaseMaturity = 50;
            consensus.CoinType = 105;
            consensus.DefaultAssumeValid = new uint256("0x55a8205ae4bbf18f4d238c43f43005bd66e0b1f679b39e2c5c62cf6903693a5e"); // 795970
            consensus.LastPOWBlock = 1000000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.MaxMoney = long.MaxValue;
            consensus.MaxReorgLength = 500;
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            consensus.SubsidyHalvingInterval = 210000;

            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;
            consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(20);

            consensus.PremineReward = Money.Coins(1000000);
            consensus.PremineHeight = 2;

            consensus.ProofOfWorkReward = Money.Coins(50);
            consensus.ProofOfStakeReward = Money.COIN;
            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));

            consensus.RuleChangeActivationThreshold = 1512; // 75% for testchains

            // Taken from StratisX.
            consensus.Options = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000
                );

            this.Consensus = consensus;

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (63) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (125) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (63 + 128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            this.Genesis = StratisMain.CreateStratisGenesisBlock(this.Consensus.ConsensusFactory, 1296688602, 414098458, 0x1d00ffff, 1, Money.Coins(50m));
            ((SmartContractBlockHeader)this.Genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");
            this.Genesis.Header.Nonce = 3; // Incremented 19/06
            this.Consensus.HashGenesisBlock = this.Genesis.Header.GetHash();
        }
    }
}