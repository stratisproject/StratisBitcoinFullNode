using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Networks.Policies;

namespace Stratis.Bitcoin.Networks
{
    public class BitcoinMain : Network
    {
        public BitcoinMain()
        {
            this.Name = "Main";
            this.AdditionalNames = new List<string> { "Mainnet" };

            this.RootFolderName = BitcoinRootFolderName;
            this.DefaultConfigFilename = BitcoinDefaultConfigFilename;
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            this.Magic = 0xD9B4BEF9;
            this.DefaultPort = 8333;
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.RPCPort = 8332;
            this.MaxTimeOffsetSeconds = BitcoinMaxTimeOffsetSeconds;
            this.MaxTipAge = BitcoinDefaultMaxTipAgeInSeconds;
            this.MinTxFee = 1000;
            this.FallbackFee = 20000;
            this.MinRelayTxFee = 1000;
            this.CoinTicker = "BTC";

            var consensusFactory = new ConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1231006505;
            this.GenesisNonce = 2083236893;
            this.GenesisBits = 0x1d00ffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            Block genesisBlock = CreateBitcoinGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 227931,
                [BuriedDeployments.BIP65] = 388381,
                [BuriedDeployments.BIP66] = 363725
            };

            var bip9Deployments = new BitcoinBIP9Deployments
            {
                [BitcoinBIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999),
                [BitcoinBIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800),
                [BitcoinBIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1479168000, 1510704000)
            };

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                coinType: 0,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016,
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 0,
                defaultAssumeValid: new uint256("0x000000000000000000174f783cc20c1415f90c4d17c9a5bcd06ba67207c9bc80"), // 518180
                maxMoney: 21000000 * Money.COIN,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Coins(50),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: new uint256("0x0000000000000000000000000000000000000000002cb971dd56d1c583c20f90"),
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (0) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (5) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // Partially obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L146
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 11111, new CheckpointInfo(new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d"))},
                { 33333, new CheckpointInfo(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6"))},
                { 74000, new CheckpointInfo(new uint256("0x0000000000573993a3c9e41ce34471c079dcf5f52a0e824a81e7f953b8661a20"))},
                { 105000, new CheckpointInfo(new uint256("0x00000000000291ce28027faea320c8d2b054b2e0fe44a773f3eefb151d6bdc97"))},
                { 134444, new CheckpointInfo(new uint256("0x00000000000005b12ffd4cd315cd34ffd4a594f430ac814c91184a0d42d2b0fe"))},
                { 168000, new CheckpointInfo(new uint256("0x000000000000099e61ea72015e79632f216fe6cb33d7899acb35b75c8303b763"))},
                { 193000, new CheckpointInfo(new uint256("0x000000000000059f452a5f7340de6682a977387c17010ff6e6c3bd83ca8b1317"))},
                { 210000, new CheckpointInfo(new uint256("0x000000000000048b95347e83192f69cf0366076336c639f9b7228e9ba171342e"))},
                { 216116, new CheckpointInfo(new uint256("0x00000000000001b4f4b433e81ee46494af945cf96014816a4e2370f11b23df4e"))},
                { 225430, new CheckpointInfo(new uint256("0x00000000000001c108384350f74090433e7fcf79a606b8e797f065b130575932"))},
                { 250000, new CheckpointInfo(new uint256("0x000000000000003887df1f29024b06fc2200b55f8af8f35453d7be294df2d214"))},
                { 279000, new CheckpointInfo(new uint256("0x0000000000000001ae8c72a0b0c301f67e3afca10e819efa9041e458e9bd7e40"))},
                { 295000, new CheckpointInfo(new uint256("0x00000000000000004d9b4ef50f0f9d686fd69db2e03af35a100370c64632a983"))},
                { 486000, new CheckpointInfo(new uint256("0x000000000000000000a2a8104d61651f76c666b70754d6e9346176385f7afa24"))},
                { 491800, new CheckpointInfo(new uint256("0x000000000000000000d80de1f855902b50941bc3a3d0f71064d9613fd3943dc4"))},
                { 550000, new CheckpointInfo(new uint256("0x000000000000000000223b7a2298fb1c6c75fb0efc28a4c56853ff4112ec6bc9"))} // 14-11-2018
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("bitcoin.sipa.be", "seed.bitcoin.sipa.be"), // Pieter Wuille
                new DNSSeedData("bluematt.me", "dnsseed.bluematt.me"), // Matt Corallo
                new DNSSeedData("dashjr.org", "dnsseed.bitcoin.dashjr.org"), // Luke Dashjr
                new DNSSeedData("bitcoinstats.com", "seed.bitcoinstats.com"), // Christian Decker
                new DNSSeedData("xf2.org", "bitseed.xf2.org"), // Jeff Garzik
                new DNSSeedData("bitcoin.jonasschnelli.ch", "seed.bitcoin.jonasschnelli.ch") // Jonas Schnelli
            };

            string[] seedNodes = { "1.34.168.128:8333", "1.202.128.218:8333", "2.30.0.210:8333", "5.9.96.203:8333", "5.45.71.130:8333", "5.45.98.141:8333", "5.102.145.68:8333", "5.135.160.77:8333", "5.189.134.246:8333", "5.199.164.132:8333", "5.249.135.102:8333", "8.19.44.110:8333", "8.22.230.8:8333", "14.200.200.145:8333", "18.228.0.188:8333", "18.228.0.200:8333", "23.24.168.97:8333", "23.28.35.227:8333", "23.92.76.170:8333", "23.99.64.119:8333", "23.228.166.128:8333", "23.229.45.32:8333", "24.8.105.128:8333", "24.16.69.137:8333", "24.94.98.96:8333", "24.102.118.7:8333", "24.118.166.228:8333", "24.122.133.49:8333", "24.166.97.162:8333", "24.213.235.242:8333", "24.226.107.64:8333", "24.228.192.171:8333", "27.140.133.18:8333", "31.41.40.25:8333", "31.43.101.59:8333", "31.184.195.181:8333", "31.193.139.66:8333", "37.200.70.102:8333", "37.205.10.151:8333", "42.3.106.227:8333", "42.60.133.106:8333", "45.56.85.231:8333", "45.56.102.228:8333", "45.79.130.235:8333", "46.28.204.61:11101", "46.38.235.229:8333", "46.59.2.74:8333", "46.101.132.37:8333", "46.101.168.50:8333", "46.163.76.230:8333", "46.166.161.103:8333", "46.182.132.100:8333", "46.223.36.94:8333", "46.227.66.132:8333", "46.227.66.138:8333", "46.239.107.74:8333", "46.249.39.100:8333", "46.250.98.108:8333", "50.7.37.114:8333", "50.81.53.151:8333", "50.115.43.253:8333", "50.116.20.87:8333", "50.116.33.92:8333", "50.125.167.245:8333", "50.143.9.51:8333", "50.188.192.133:8333", "54.77.162.76:8333", "54.153.97.109:8333", "54.165.192.125:8333", "58.96.105.85:8333", "59.167.196.135:8333", "60.29.227.163:8333", "61.35.225.19:8333", "62.43.130.178:8333", "62.109.49.26:8333", "62.202.0.97:8333", "62.210.66.227:8333", "62.210.192.169:8333", "64.74.98.205:8333", "64.156.193.100:8333", "64.203.102.86:8333", "64.229.142.48:8333", "65.96.193.165:8333", "66.30.3.7:8333", "66.114.33.49:8333", "66.118.133.194:8333", "66.135.10.126:8333", "66.172.10.4:8333", "66.194.38.250:8333", "66.194.38.253:8333", "66.215.192.104:8333", "67.60.98.115:8333", "67.164.35.36:8333", "67.191.162.244:8333", "67.207.195.77:8333", "67.219.233.140:8333", "67.221.193.55:8333", "67.228.162.228:8333", "68.50.67.199:8333", "68.62.3.203:8333", "68.65.205.226:9000", "68.106.42.191:8333", "68.150.181.198:8333", "68.196.196.106:8333", "68.224.194.81:8333", "69.46.5.194:8333", "69.50.171.238:8333", "69.64.43.152:8333", "69.65.41.13:8333", "69.90.132.200:8333", "69.143.1.243:8333", "69.146.98.216:8333", "69.165.246.38:8333", "69.207.6.135:8333", "69.251.208.26:8333", "70.38.1.101:8333", "70.38.9.66:8333", "70.90.2.18:8333", "71.58.228.226:8333", "71.199.11.189:8333", "71.199.193.202:8333", "71.205.232.181:8333", "71.236.200.162:8333", "72.24.73.186:8333", "72.52.130.110:8333", "72.53.111.37:8333", "72.235.38.70:8333", "73.31.171.149:8333", "73.32.137.72:8333", "73.137.133.238:8333", "73.181.192.103:8333", "73.190.2.60:8333", "73.195.192.137:8333", "73.222.35.117:8333", "74.57.199.180:8333", "74.82.233.205:8333", "74.85.66.82:8333", "74.101.224.127:8333", "74.113.69.16:8333", "74.122.235.68:8333", "74.193.68.141:8333", "74.208.164.219:8333", "75.100.37.122:8333", "75.145.149.169:8333", "75.168.34.20:8333", "76.20.44.240:8333", "76.100.70.17:8333", "76.168.3.239:8333", "76.186.140.103:8333", "77.92.68.221:8333", "77.109.101.142:8333", "77.110.11.86:8333", "77.242.108.18:8333", "78.46.96.150:9020", "78.84.100.95:8333", "79.132.230.144:8333", "79.133.43.63:8333", "79.160.76.153:8333", "79.169.34.24:8333", "79.188.7.78:8333", "80.217.226.25:8333", "80.223.100.179:8333", "80.240.129.221:8333", "81.1.173.243:8333", "81.7.11.50:8333", "81.7.16.17:8333", "81.66.111.3:8333", "81.80.9.71:8333", "81.140.43.138:8333", "81.171.34.37:8333", "81.174.247.50:8333", "81.181.155.53:8333", "81.184.5.253:8333", "81.187.69.130:8333", "81.230.3.84:8333", "82.42.128.51:8333", "82.74.226.21:8333", "82.142.75.50:8333", "82.199.102.10:8333", "82.200.205.30:8333", "82.221.108.21:8333", "82.221.128.35:8333", "82.238.124.41:8333", "82.242.0.245:8333", "83.76.123.110:8333", "83.150.9.196:8333", "83.162.196.192:8333", "83.162.234.224:8333", "83.170.104.91:8333", "83.255.66.118:8334", "84.2.34.104:8333", "84.45.98.91:8333", "84.47.161.150:8333", "84.212.192.131:8333", "84.215.169.101:8333", "84.238.140.176:8333", "84.245.71.31:8333", "85.17.4.212:8333", "85.114.128.134:8333", "85.159.237.191:8333", "85.166.130.189:8333", "85.199.4.228:8333", "85.214.66.168:8333", "85.214.195.210:8333", "85.229.0.73:8333", "86.21.96.45:8333", "87.48.42.199:8333", "87.81.143.82:8333", "87.81.251.72:8333", "87.104.24.185:8333", "87.104.168.104:8333", "87.117.234.71:8333", "87.118.96.197:8333", "87.145.12.57:8333", "87.159.170.190:8333", "88.150.168.160:8333", "88.208.0.79:8333", "88.208.0.149:8333", "88.214.194.226:8343", "89.1.11.32:8333", "89.36.235.108:8333", "89.67.96.2:15321", "89.98.16.41:8333", "89.108.72.195:8333", "89.156.35.157:8333", "89.163.227.28:8333", "89.212.33.237:8333", "89.212.160.165:8333", "89.231.96.83:8333", "89.248.164.64:8333", "90.149.193.199:8333", "91.77.239.245:8333", "91.106.194.97:8333", "91.126.77.77:8333", "91.134.38.195:8333", "91.156.97.181:8333", "91.207.68.144:8333", "91.209.77.101:8333", "91.214.200.205:8333", "91.220.131.242:8333", "91.220.163.18:8333", "91.233.23.35:8333", "92.13.96.93:8333", "92.14.74.114:8333", "92.27.7.209:8333", "92.221.228.13:8333", "92.255.207.73:8333", "93.72.167.148:8333", "93.74.163.234:8333", "93.123.174.66:8333", "93.152.166.29:8333", "93.181.45.188:8333", "94.19.12.244:8333", "94.190.227.112:8333", "94.198.135.29:8333", "94.224.162.65:8333", "94.226.107.86:8333", "94.242.198.161:8333", "95.31.10.209:8333", "95.65.72.244:8333", "95.84.162.95:8333", "95.90.139.46:8333", "95.183.49.27:8005", "95.215.47.133:8333", "96.23.67.85:8333", "96.44.166.190:8333", "97.93.225.74:8333", "98.26.0.34:8333", "98.27.225.102:8333", "98.229.117.229:8333", "98.249.68.125:8333", "98.255.5.155:8333", "99.101.240.114:8333", "101.100.174.138:8333", "101.251.203.6:8333", "103.3.60.61:8333", "103.30.42.189:8333", "103.224.165.48:8333", "104.36.83.233:8333", "104.37.129.22:8333", "104.54.192.251:8333", "104.128.228.252:8333", "104.128.230.185:8334", "104.130.161.47:8333", "104.131.33.60:8333", "104.143.0.156:8333", "104.156.111.72:8333", "104.167.111.84:8333", "104.193.40.248:8333", "104.197.7.174:8333", "104.197.8.250:8333", "104.223.1.133:8333", "104.236.97.140:8333", "104.238.128.214:8333", "104.238.130.182:8333", "106.38.234.84:8333", "106.185.36.204:8333", "107.6.4.145:8333", "107.150.2.6:8333", "107.150.40.234:8333", "107.155.108.130:8333", "107.161.182.115:8333", "107.170.66.231:8333", "107.190.128.226:8333", "107.191.106.115:8333", "108.16.2.61:8333", "109.70.4.168:8333", "109.162.35.196:8333", "109.163.235.239:8333", "109.190.196.220:8333", "109.191.39.60:8333", "109.234.106.191:8333", "109.238.81.82:8333", "114.76.147.27:8333", "115.28.224.127:8333", "115.68.110.82:18333", "118.97.79.218:8333", "118.189.207.197:8333", "119.228.96.233:8333", "120.147.178.81:8333", "121.41.123.5:8333", "121.67.5.230:8333", "122.107.143.110:8333", "123.2.170.98:8333", "123.110.65.94:8333", "123.193.139.19:8333", "125.239.160.41:8333", "128.101.162.193:8333", "128.111.73.10:8333", "128.140.229.73:8333", "128.175.195.31:8333", "128.199.107.63:8333", "128.199.192.153:8333", "128.253.3.193:20020", "129.123.7.7:8333", "130.89.160.234:8333", "131.72.139.164:8333", "131.191.112.98:8333", "133.1.134.162:8333", "134.19.132.53:8333", "137.226.34.42:8333", "141.41.2.172:8333", "141.255.128.204:8333", "142.217.12.106:8333", "143.215.129.126:8333", "146.0.32.101:8337", "147.229.13.199:8333", "149.210.133.244:8333", "149.210.162.187:8333", "150.101.163.241:8333", "151.236.11.189:8333", "153.121.66.211:8333", "154.20.2.139:8333", "159.253.23.132:8333", "162.209.106.123:8333", "162.210.198.184:8333", "162.218.65.121:8333", "162.222.161.49:8333", "162.243.132.6:8333", "162.243.132.58:8333", "162.248.99.164:53011", "162.248.102.117:8333", "163.158.35.110:8333", "164.15.10.189:8333", "164.40.134.171:8333", "166.230.71.67:8333", "167.160.161.199:8333", "168.103.195.250:8333", "168.144.27.112:8333", "168.158.129.29:8333", "170.75.162.86:8333", "172.90.99.174:8333", "172.245.5.156:8333", "173.23.166.47:8333", "173.32.11.194:8333", "173.34.203.76:8333", "173.171.1.52:8333", "173.175.136.13:8333", "173.230.228.139:8333", "173.247.193.70:8333", "174.49.132.28:8333", "174.52.202.72:8333", "174.53.76.87:8333", "174.109.33.28:8333", "176.28.12.169:8333", "176.35.182.214:8333", "176.36.33.113:8333", "176.36.33.121:8333", "176.58.96.173:8333", "176.121.76.84:8333", "178.62.70.16:8333", "178.62.111.26:8333", "178.76.169.59:8333", "178.79.131.32:8333", "178.162.199.216:8333", "178.175.134.35:8333", "178.248.111.4:8333", "178.254.1.170:8333", "178.254.34.161:8333", "179.43.143.120:8333", "179.208.156.198:8333", "180.200.128.58:8333", "183.78.169.108:8333", "183.96.96.152:8333", "184.68.2.46:8333", "184.73.160.160:8333", "184.94.227.58:8333", "184.152.68.163:8333", "185.7.35.114:8333", "185.28.76.179:8333", "185.31.160.202:8333", "185.45.192.129:8333", "185.66.140.15:8333", "186.2.167.23:8333", "186.220.101.142:8333", "188.26.5.33:8333", "188.75.136.146:8333", "188.120.194.140:8333", "188.121.5.150:8333", "188.138.0.114:8333", "188.138.33.239:8333", "188.166.0.82:8333", "188.182.108.129:8333", "188.191.97.208:8333", "188.226.198.102:8001", "190.10.9.217:8333", "190.75.143.144:8333", "190.139.102.146:8333", "191.237.64.28:8333", "192.3.131.61:8333", "192.99.225.3:8333", "192.110.160.122:8333", "192.146.137.1:8333", "192.183.198.204:8333", "192.203.228.71:8333", "193.0.109.3:8333", "193.12.238.204:8333", "193.91.200.85:8333", "193.234.225.156:8333", "194.6.233.38:8333", "194.63.143.136:8333", "194.126.100.246:8333", "195.134.99.195:8333", "195.159.111.98:8333", "195.159.226.139:8333", "195.197.175.190:8333", "198.48.199.108:8333", "198.57.208.134:8333", "198.57.210.27:8333", "198.62.109.223:8333", "198.167.140.8:8333", "198.167.140.18:8333", "199.91.173.234:8333", "199.127.226.245:8333", "199.180.134.116:8333", "200.7.96.99:8333", "201.160.106.86:8333", "202.55.87.45:8333", "202.60.68.242:8333", "202.60.69.232:8333", "202.124.109.103:8333", "203.30.197.77:8333", "203.88.160.43:8333", "203.151.140.14:8333", "203.219.14.204:8333", "205.147.40.62:8333", "207.235.39.214:8333", "207.244.73.8:8333", "208.12.64.225:8333", "208.76.200.200:8333", "209.40.96.121:8333", "209.126.107.176:8333", "209.141.40.149:8333", "209.190.75.59:8333", "209.208.111.142:8333", "210.54.34.164:8333", "211.72.66.229:8333", "212.51.144.42:8333", "212.112.33.157:8333", "212.116.72.63:8333", "212.126.14.122:8333", "213.66.205.194:8333", "213.111.196.21:8333", "213.122.107.102:8333", "213.136.75.175:8333", "213.155.7.24:8333", "213.163.64.31:8333", "213.163.64.208:8333", "213.165.86.136:8333", "213.184.8.22:8333", "216.15.78.182:8333", "216.55.143.154:8333", "216.115.235.32:8333", "216.126.226.166:8333", "216.145.67.87:8333", "216.169.141.169:8333", "216.249.92.230:8333", "216.250.138.230:8333", "217.20.171.43:8333", "217.23.2.71:8333", "217.23.2.242:8333", "217.25.9.76:8333", "217.40.226.169:8333", "217.123.98.9:8333", "217.155.36.62:8333", "217.172.32.18:20993", "218.61.196.202:8333", "218.231.205.41:8333", "220.233.77.200:8333", "223.18.226.85:8333", "223.197.203.82:8333", "223.255.166.142:8333" };
            this.SeedNodes = ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"));
        }

        /// <summary> Bitcoin maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BitcoinMaxTimeOffsetSeconds = 70 * 60;

        /// <summary> Bitcoin default value for the maximum tip age in seconds to consider the node in initial block download (24 hours). </summary>
        public const int BitcoinDefaultMaxTipAgeInSeconds = 24 * 60 * 60;

        /// <summary> The name of the root folder containing the different Bitcoin blockchains (Main, TestNet, RegTest). </summary>
        public const string BitcoinRootFolderName = "bitcoin";

        /// <summary> The default name used for the Bitcoin configuration file. </summary>
        public const string BitcoinDefaultConfigFilename = "bitcoin.conf";

        public static Block CreateBitcoinGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "The Times 03/Jan/2009 Chancellor on brink of second bailout for banks";
            var genesisOutputScript = new Script(Op.GetPushOp(Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f")), OpcodeType.OP_CHECKSIG);

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(486604799), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)4 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
                ScriptPubKey = genesisOutputScript
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
    }
}