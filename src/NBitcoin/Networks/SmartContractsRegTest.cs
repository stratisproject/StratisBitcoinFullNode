using System;
using System.Collections.Generic;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin.Networks
{
    public sealed class SmartContractsRegTest : Network
    {
        /// <summary>
        /// Took the 'InitReg' from above and adjusted it slightly (set a static flag + removed the hash check)
        /// </summary>
        public SmartContractsRegTest()
        {
            this.Name = "SmartContractsRegTest";
            this.RootFolderName = StratisRootFolderName;
            this.DefaultConfigFilename = StratisDefaultConfigFilename;
            this.Magic = 0xDAB5BFFA;
            this.DefaultPort = 18444;
            this.RPCPort = 18332;
            this.MaxTipAge = BitcoinDefaultMaxTipAgeInSeconds;
            this.MinTxFee = 1000;
            this.FallbackFee = 20000;
            this.MinRelayTxFee = 1000;

            this.Consensus.ConsensusFactory = new SmartContractConsensusFactory() { Consensus = this.Consensus };

            this.Consensus.SubsidyHalvingInterval = 150;
            this.Consensus.MajorityEnforceBlockUpgrade = 750;
            this.Consensus.MajorityRejectBlockOutdated = 950;
            this.Consensus.MajorityWindow = 1000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP34] = 100000000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP65] = 100000000;
            this.Consensus.BuriedDeployments[BuriedDeployments.BIP66] = 100000000;
            this.Consensus.BIP34Hash = new uint256();
            this.Consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.Consensus.MinimumChainWork = uint256.Zero;
            this.Consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            this.Consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            this.Consensus.PowAllowMinDifficultyBlocks = true;
            this.Consensus.PowNoRetargeting = true;
            this.Consensus.RuleChangeActivationThreshold = 108;
            this.Consensus.MinerConfirmationWindow = 144;
            this.Consensus.IsSmartContracts = true;

            this.Consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 0, 999999999);
            this.Consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 0, 999999999);
            this.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999);

            this.Genesis = CreateBitcoinGenesisBlock(this.Consensus.ConsensusFactory, 1296688602, 2, 0x207fffff, 1, Money.Coins(50m));
            ((SmartContractBlockHeader)this.Genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");
            this.Consensus.HashGenesisBlock = this.Genesis.Header.GetHash();

            this.Consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("93867319cf92c86f957a9652c1fbe7cc8cbe70c53a915ac96ee7c59cb80f94b4"));

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

            var encoder = new Bech32Encoder("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();
        }
    }
}