using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.SmartContracts.Networks.Policies;

namespace Stratis.Sidechains.Networks.CirrusV2
{
    public class CirrusRegTest : PoANetwork, ISignedCodePubKeyHolder
    {
        public IList<Mnemonic> FederationMnemonics { get; }

        /// <summary> The name of the root folder containing the different federated peg blockchains.</summary>
        private const string NetworkRootFolderName = "cirrus";

        /// <summary> The default name used for the federated peg configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "cirrus.conf";

        // public IList<Mnemonic> FederationMnemonics { get; }
        public IList<Key> FederationKeys { get; private set; }

        public Key SigningContractPrivKey { get; }

        public PubKey SigningContractPubKey { get; }

        internal CirrusRegTest()
        {
            this.SigningContractPrivKey = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom").DeriveExtKey().PrivateKey;
            this.SigningContractPubKey = this.SigningContractPrivKey.PubKey;

            this.Name = nameof(CirrusRegTest);
            this.CoinTicker = "TFPG";
            this.Magic = 0x522357C;
            this.DefaultPort = 26179;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.RPCPort = 26175;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;

            var consensusFactory = new SmartContractPoAConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1513622125;
            this.GenesisNonce = 1560058197;
            this.GenesisBits = 402691653;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            string coinbaseText = "https://news.bitcoin.com/markets-update-cryptocurrencies-shed-billions-in-bloody-sell-off/";
            Block genesisBlock = CirrusNetwork.CreateGenesis(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward, coinbaseText);

            this.Genesis = genesisBlock;

            this.FederationMnemonics = new[] {
                   "ensure feel swift crucial bridge charge cloud tell hobby twenty people mandate",
                   "quiz sunset vote alley draw turkey hill scrap lumber game differ fiction",
                   "exchange rent bronze pole post hurry oppose drama eternal voice client state"
               }.Select(m => new Mnemonic(m, Wordlist.English)).ToList();

            this.FederationKeys = this.FederationMnemonics.Select(m => m.DeriveExtKey().PrivateKey).ToList();

            List<PubKey> federationPubKeys = this.FederationKeys.Select(k => k.PubKey).ToList();

            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5,
                federationPublicKeys: federationPubKeys,
                targetSpacingSeconds: 4, // For integration tests - avoid FastMining
                votingEnabled: false
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new NoBIP9Deployments();

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 400,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 0, // No max reorg limit on PoA networks.
                defaultAssumeValid: null,
                maxMoney: Money.Coins(20_000_000),
                coinbaseMaturity: 1,
                premineHeight: 2,
                premineReward: Money.Coins(20_000_000),
                proofOfWorkReward: Money.Coins(0),
                powTargetTimespan: TimeSpan.FromDays(14), // two weeks
                powTargetSpacing: TimeSpan.FromMinutes(1),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: true,
                powNoRetargeting: true,
                powLimit: null,
                minimumChainWork: null,
                isProofOfStake: false,
                lastPowBlock: 0,
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero
            );

            // Same as current smart contracts test networks to keep tests working
            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 55 }; // P
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 117 }; // p
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

            this.StandardScriptsRegistry = new SmartContractsStandardScriptsRegistry();

            // TODO: Do we need Asserts for block hash
        }
    }
}
