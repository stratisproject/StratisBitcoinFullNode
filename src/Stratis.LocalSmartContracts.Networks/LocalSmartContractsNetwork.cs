using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Policies;
using Stratis.Bitcoin.Features.SmartContracts.PoA;

namespace Stratis.LocalSmartContracts.Networks
{
    /// <summary>
    /// A network
    /// </summary>
    public class LocalSmartContracts : PoANetwork
    {
        /// <summary> The name of the root folder containing the different federated peg blockchains.</summary>
        private const string NetworkRootFolderName = "LocalSmartContracts";

        /// <summary> The default name used for the federated peg configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "LocalSmartContracts.conf";

        public LocalSmartContracts()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x76;
            messageStart[1] = 0x36;
            messageStart[2] = 0x23;
            messageStart[3] = 0x06;
            uint magic = BitConverter.ToUInt32(messageStart, 0);

            this.Name = "LSC_Main"; // The only network configuration for LSCs - no Testnet etc. required. 
            this.CoinTicker = "LSC";
            this.Magic = magic;
            this.DefaultPort = 16179;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.RPCPort = 16175;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;

            var consensusFactory = new SmartContractPoAConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1551703905;
            this.GenesisNonce = 761900;
            this.GenesisBits = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            string coinbaseText = "https://www.bbc.co.uk/news/world-africa-45889707?intlink_from_url=https://www.bbc.co.uk/news/topics/cyd7z4rvdm3t/crypto-currency&link_location=live-reporting-story";
            Block genesisBlock = CreateLSCGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward, coinbaseText);

            this.Genesis = genesisBlock;

            // Configure federation public keys used to sign blocks.
            // Keep in mind that order in which keys are added to this list is important
            // and should be the same for all nodes operating on this network.
            var federationPublicKeys = new List<PubKey>
            {
                new PubKey("02f5b2a2fc2aa9f2ab85e9727720f9b280ed937f897e444810abaada26738b13c4"),
            };

            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5,
                federationPublicKeys: federationPublicKeys,
                targetSpacingSeconds: 16,
                votingEnabled: true
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new NoBIP9Deployments();

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 401,
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
                maxReorgLength: 500,
                defaultAssumeValid: null,
                maxMoney: Money.Coins(100_000_000),
                coinbaseMaturity: 1,
                premineHeight: 2,
                premineReward: Money.Coins(100_000_000),
                proofOfWorkReward: Money.Coins(0),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(60),
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
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { 28 }; // C
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { 88 }; // c
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

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            Bech32Encoder encoder = Encoders.Bech32("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // No DNS seeds.
            this.DNSSeeds = new List<DNSSeedData> { };

            // No seed nodes.
            string[] seedNodes = { };
            this.SeedNodes = this.ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            this.StandardScriptsRegistry = new PoAStandardScriptsRegistry();
            
            // Console.WriteLine("Genesis Block Hash: '{0}'", genesisBlock.GetHash().ToString());
            // Console.WriteLine("Hash Merkle Root: '{0}'", genesisBlock.Header.HashMerkleRoot.ToString());
            
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x1228f793ede0ae6aab9c129d021be95cf233d5a73de2ca123cb920435b9e7872"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x6abb8137b005211ddd2878aebd97b69c89a7115ca5020e8fb3a98cc9a8bab09c"));

            if ((this.ConsensusOptions.GenesisFederationPublicKeys == null) || (this.ConsensusOptions.GenesisFederationPublicKeys.Count == 0))
            {
                throw new Exception("No keys for initial federation are configured!");
            }
        }
        
        protected static Block CreateLSCGenesisBlock(SmartContractPoAConsensusFactory consensusFactory, uint genesisTime, uint nonce, uint bits, int version, Money reward, string coinbaseText)
        {
            Transaction genesisTransaction = consensusFactory.CreateTransaction();
            genesisTransaction.Time = genesisTime;
            genesisTransaction.Version = 1;
            genesisTransaction.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(coinbaseText)))
            });

            genesisTransaction.AddOutput(new TxOut()
            {
                Value = reward
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(genesisTime);
            genesis.Header.Bits = bits;
            genesis.Header.Nonce = nonce;
            genesis.Header.Version = version;
            genesis.Transactions.Add(genesisTransaction);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            ((SmartContractPoABlockHeader)genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");

            return genesis;
        }
    }
}