using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public partial class Network
    {
        static Network()
        {
            // initialize the networks
            Network main = Network.Main;
            Network testNet = Network.TestNet;
            Network regTest = Network.RegTest;
        }

        /// <summary> Bitcoin maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BitcoinMaxTimeOffsetSeconds = 70 * 60;

        /// <summary> Stratis maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int StratisMaxTimeOffsetSeconds = 25 * 60;

        /// <summary> Bitcoin default value for the maximum tip age in seconds to consider the node in initial block download (24 hours). </summary>
        public const int BitcoinDefaultMaxTipAgeInSeconds = 24 * 60 * 60;

        /// <summary> Stratis default value for the maximum tip age in seconds to consider the node in initial block download (2 hours). </summary>
        public const int StratisDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary> The name of the root folder containing the different Bitcoin blockchains (Main, TestNet, RegTest). </summary>
        public const string BitcoinRootFolderName = "bitcoin";

        /// <summary> The default name used for the Bitcoin configuration file. </summary>
        public const string BitcoinDefaultConfigFilename = "bitcoin.conf";

        /// <summary> The name of the root folder containing the different Stratis blockchains (StratisMain, StratisTest, StratisRegTest). </summary>
        public const string StratisRootFolderName = "stratis";

        /// <summary> The default name used for the Stratis configuration file. </summary>
        public const string StratisDefaultConfigFilename = "stratis.conf";

        public static Network Main => Network.GetNetwork("Main") ?? InitMain();

        public static Network TestNet => Network.GetNetwork("TestNet") ?? InitTest();

        public static Network RegTest => Network.GetNetwork("RegTest") ?? InitReg();

        public static Network StratisMain => Network.GetNetwork("StratisMain") ?? InitStratisMain();

        public static Network StratisTest => Network.GetNetwork("StratisTest") ?? InitStratisTest();

        public static Network StratisRegTest => Network.GetNetwork("StratisRegTest") ?? InitStratisRegTest();

        private static Network InitMain()
        {
            Network network = new Network
            {
                Name = "Main",
                RootFolderName = BitcoinRootFolderName,
                DefaultConfigFilename = BitcoinDefaultConfigFilename
            };

            Consensus consensus = network.consensus;

            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 227931;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 388381;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 363725;
            consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            consensus.PowLimit = new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.MinimumChainWork = new uint256("0x0000000000000000000000000000000000000000002cb971dd56d1c583c20f90");
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.PowAllowMinDifficultyBlocks = false;
            consensus.PowNoRetargeting = false;
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing

            consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
            consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800);
            consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1479168000, 1510704000);

            consensus.CoinType = 0;

            consensus.DefaultAssumeValid = new uint256("0x000000000000000000174f783cc20c1415f90c4d17c9a5bcd06ba67207c9bc80"); // 518180

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            network.magic = 0xD9B4BEF9;
            network.alertPubKeyArray = Encoders.Hex.DecodeData("04fc9702847840aaf195de8442ebecedf5b095cdbb9bc716bda9110971b28a49e0ead8564ff0db22209e0374782c093bb899692d524e9d6a6956e7c5ecbcd68284");
            network.DefaultPort = 8333;
            network.RPCPort = 8332;

            network.genesis = CreateGenesisBlock(consensus.ConsensusFactory, 1231006505, 2083236893, 0x1d00ffff, 1, Money.Coins(50m));
            consensus.HashGenesisBlock = network.genesis.GetHash();
            Assert(consensus.HashGenesisBlock == uint256.Parse("0x000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"));
            Assert(network.genesis.Header.HashMerkleRoot == uint256.Parse("0x4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b"));
            network.seeds.Add(new DNSSeedData("bitcoin.sipa.be", "seed.bitcoin.sipa.be")); // Pieter Wuille
            network.seeds.Add(new DNSSeedData("bluematt.me", "dnsseed.bluematt.me")); // Matt Corallo
            network.seeds.Add(new DNSSeedData("dashjr.org", "dnsseed.bitcoin.dashjr.org")); // Luke Dashjr
            network.seeds.Add(new DNSSeedData("bitcoinstats.com", "seed.bitcoinstats.com")); // Christian Decker
            network.seeds.Add(new DNSSeedData("xf2.org", "bitseed.xf2.org")); // Jeff Garzik
            network.seeds.Add(new DNSSeedData("bitcoin.jonasschnelli.ch", "seed.bitcoin.jonasschnelli.ch")); // Jonas Schnelli
            network.base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (0) };
            network.base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (5) };
            network.base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (128) };
            network.base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            network.base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            network.base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            network.base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            network.base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            network.base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            network.base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            network.base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            network.base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("bc");
            network.bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // Convert the pnSeeds array into usable address objects.
            string[] pnSeed = new[] { "1.34.168.128:8333", "1.202.128.218:8333", "2.30.0.210:8333", "5.9.96.203:8333", "5.45.71.130:8333", "5.45.98.141:8333", "5.102.145.68:8333", "5.135.160.77:8333", "5.189.134.246:8333", "5.199.164.132:8333", "5.249.135.102:8333", "8.19.44.110:8333", "8.22.230.8:8333", "14.200.200.145:8333", "18.228.0.188:8333", "18.228.0.200:8333", "23.24.168.97:8333", "23.28.35.227:8333", "23.92.76.170:8333", "23.99.64.119:8333", "23.228.166.128:8333", "23.229.45.32:8333", "24.8.105.128:8333", "24.16.69.137:8333", "24.94.98.96:8333", "24.102.118.7:8333", "24.118.166.228:8333", "24.122.133.49:8333", "24.166.97.162:8333", "24.213.235.242:8333", "24.226.107.64:8333", "24.228.192.171:8333", "27.140.133.18:8333", "31.41.40.25:8333", "31.43.101.59:8333", "31.184.195.181:8333", "31.193.139.66:8333", "37.200.70.102:8333", "37.205.10.151:8333", "42.3.106.227:8333", "42.60.133.106:8333", "45.56.85.231:8333", "45.56.102.228:8333", "45.79.130.235:8333", "46.28.204.61:11101", "46.38.235.229:8333", "46.59.2.74:8333", "46.101.132.37:8333", "46.101.168.50:8333", "46.163.76.230:8333", "46.166.161.103:8333", "46.182.132.100:8333", "46.223.36.94:8333", "46.227.66.132:8333", "46.227.66.138:8333", "46.239.107.74:8333", "46.249.39.100:8333", "46.250.98.108:8333", "50.7.37.114:8333", "50.81.53.151:8333", "50.115.43.253:8333", "50.116.20.87:8333", "50.116.33.92:8333", "50.125.167.245:8333", "50.143.9.51:8333", "50.188.192.133:8333", "54.77.162.76:8333", "54.153.97.109:8333", "54.165.192.125:8333", "58.96.105.85:8333", "59.167.196.135:8333", "60.29.227.163:8333", "61.35.225.19:8333", "62.43.130.178:8333", "62.109.49.26:8333", "62.202.0.97:8333", "62.210.66.227:8333", "62.210.192.169:8333", "64.74.98.205:8333", "64.156.193.100:8333", "64.203.102.86:8333", "64.229.142.48:8333", "65.96.193.165:8333", "66.30.3.7:8333", "66.114.33.49:8333", "66.118.133.194:8333", "66.135.10.126:8333", "66.172.10.4:8333", "66.194.38.250:8333", "66.194.38.253:8333", "66.215.192.104:8333", "67.60.98.115:8333", "67.164.35.36:8333", "67.191.162.244:8333", "67.207.195.77:8333", "67.219.233.140:8333", "67.221.193.55:8333", "67.228.162.228:8333", "68.50.67.199:8333", "68.62.3.203:8333", "68.65.205.226:9000", "68.106.42.191:8333", "68.150.181.198:8333", "68.196.196.106:8333", "68.224.194.81:8333", "69.46.5.194:8333", "69.50.171.238:8333", "69.64.43.152:8333", "69.65.41.13:8333", "69.90.132.200:8333", "69.143.1.243:8333", "69.146.98.216:8333", "69.165.246.38:8333", "69.207.6.135:8333", "69.251.208.26:8333", "70.38.1.101:8333", "70.38.9.66:8333", "70.90.2.18:8333", "71.58.228.226:8333", "71.199.11.189:8333", "71.199.193.202:8333", "71.205.232.181:8333", "71.236.200.162:8333", "72.24.73.186:8333", "72.52.130.110:8333", "72.53.111.37:8333", "72.235.38.70:8333", "73.31.171.149:8333", "73.32.137.72:8333", "73.137.133.238:8333", "73.181.192.103:8333", "73.190.2.60:8333", "73.195.192.137:8333", "73.222.35.117:8333", "74.57.199.180:8333", "74.82.233.205:8333", "74.85.66.82:8333", "74.101.224.127:8333", "74.113.69.16:8333", "74.122.235.68:8333", "74.193.68.141:8333", "74.208.164.219:8333", "75.100.37.122:8333", "75.145.149.169:8333", "75.168.34.20:8333", "76.20.44.240:8333", "76.100.70.17:8333", "76.168.3.239:8333", "76.186.140.103:8333", "77.92.68.221:8333", "77.109.101.142:8333", "77.110.11.86:8333", "77.242.108.18:8333", "78.46.96.150:9020", "78.84.100.95:8333", "79.132.230.144:8333", "79.133.43.63:8333", "79.160.76.153:8333", "79.169.34.24:8333", "79.188.7.78:8333", "80.217.226.25:8333", "80.223.100.179:8333", "80.240.129.221:8333", "81.1.173.243:8333", "81.7.11.50:8333", "81.7.16.17:8333", "81.66.111.3:8333", "81.80.9.71:8333", "81.140.43.138:8333", "81.171.34.37:8333", "81.174.247.50:8333", "81.181.155.53:8333", "81.184.5.253:8333", "81.187.69.130:8333", "81.230.3.84:8333", "82.42.128.51:8333", "82.74.226.21:8333", "82.142.75.50:8333", "82.199.102.10:8333", "82.200.205.30:8333", "82.221.108.21:8333", "82.221.128.35:8333", "82.238.124.41:8333", "82.242.0.245:8333", "83.76.123.110:8333", "83.150.9.196:8333", "83.162.196.192:8333", "83.162.234.224:8333", "83.170.104.91:8333", "83.255.66.118:8334", "84.2.34.104:8333", "84.45.98.91:8333", "84.47.161.150:8333", "84.212.192.131:8333", "84.215.169.101:8333", "84.238.140.176:8333", "84.245.71.31:8333", "85.17.4.212:8333", "85.114.128.134:8333", "85.159.237.191:8333", "85.166.130.189:8333", "85.199.4.228:8333", "85.214.66.168:8333", "85.214.195.210:8333", "85.229.0.73:8333", "86.21.96.45:8333", "87.48.42.199:8333", "87.81.143.82:8333", "87.81.251.72:8333", "87.104.24.185:8333", "87.104.168.104:8333", "87.117.234.71:8333", "87.118.96.197:8333", "87.145.12.57:8333", "87.159.170.190:8333", "88.150.168.160:8333", "88.208.0.79:8333", "88.208.0.149:8333", "88.214.194.226:8343", "89.1.11.32:8333", "89.36.235.108:8333", "89.67.96.2:15321", "89.98.16.41:8333", "89.108.72.195:8333", "89.156.35.157:8333", "89.163.227.28:8333", "89.212.33.237:8333", "89.212.160.165:8333", "89.231.96.83:8333", "89.248.164.64:8333", "90.149.193.199:8333", "91.77.239.245:8333", "91.106.194.97:8333", "91.126.77.77:8333", "91.134.38.195:8333", "91.156.97.181:8333", "91.207.68.144:8333", "91.209.77.101:8333", "91.214.200.205:8333", "91.220.131.242:8333", "91.220.163.18:8333", "91.233.23.35:8333", "92.13.96.93:8333", "92.14.74.114:8333", "92.27.7.209:8333", "92.221.228.13:8333", "92.255.207.73:8333", "93.72.167.148:8333", "93.74.163.234:8333", "93.123.174.66:8333", "93.152.166.29:8333", "93.181.45.188:8333", "94.19.12.244:8333", "94.190.227.112:8333", "94.198.135.29:8333", "94.224.162.65:8333", "94.226.107.86:8333", "94.242.198.161:8333", "95.31.10.209:8333", "95.65.72.244:8333", "95.84.162.95:8333", "95.90.139.46:8333", "95.183.49.27:8005", "95.215.47.133:8333", "96.23.67.85:8333", "96.44.166.190:8333", "97.93.225.74:8333", "98.26.0.34:8333", "98.27.225.102:8333", "98.229.117.229:8333", "98.249.68.125:8333", "98.255.5.155:8333", "99.101.240.114:8333", "101.100.174.138:8333", "101.251.203.6:8333", "103.3.60.61:8333", "103.30.42.189:8333", "103.224.165.48:8333", "104.36.83.233:8333", "104.37.129.22:8333", "104.54.192.251:8333", "104.128.228.252:8333", "104.128.230.185:8334", "104.130.161.47:8333", "104.131.33.60:8333", "104.143.0.156:8333", "104.156.111.72:8333", "104.167.111.84:8333", "104.193.40.248:8333", "104.197.7.174:8333", "104.197.8.250:8333", "104.223.1.133:8333", "104.236.97.140:8333", "104.238.128.214:8333", "104.238.130.182:8333", "106.38.234.84:8333", "106.185.36.204:8333", "107.6.4.145:8333", "107.150.2.6:8333", "107.150.40.234:8333", "107.155.108.130:8333", "107.161.182.115:8333", "107.170.66.231:8333", "107.190.128.226:8333", "107.191.106.115:8333", "108.16.2.61:8333", "109.70.4.168:8333", "109.162.35.196:8333", "109.163.235.239:8333", "109.190.196.220:8333", "109.191.39.60:8333", "109.234.106.191:8333", "109.238.81.82:8333", "114.76.147.27:8333", "115.28.224.127:8333", "115.68.110.82:18333", "118.97.79.218:8333", "118.189.207.197:8333", "119.228.96.233:8333", "120.147.178.81:8333", "121.41.123.5:8333", "121.67.5.230:8333", "122.107.143.110:8333", "123.2.170.98:8333", "123.110.65.94:8333", "123.193.139.19:8333", "125.239.160.41:8333", "128.101.162.193:8333", "128.111.73.10:8333", "128.140.229.73:8333", "128.175.195.31:8333", "128.199.107.63:8333", "128.199.192.153:8333", "128.253.3.193:20020", "129.123.7.7:8333", "130.89.160.234:8333", "131.72.139.164:8333", "131.191.112.98:8333", "133.1.134.162:8333", "134.19.132.53:8333", "137.226.34.42:8333", "141.41.2.172:8333", "141.255.128.204:8333", "142.217.12.106:8333", "143.215.129.126:8333", "146.0.32.101:8337", "147.229.13.199:8333", "149.210.133.244:8333", "149.210.162.187:8333", "150.101.163.241:8333", "151.236.11.189:8333", "153.121.66.211:8333", "154.20.2.139:8333", "159.253.23.132:8333", "162.209.106.123:8333", "162.210.198.184:8333", "162.218.65.121:8333", "162.222.161.49:8333", "162.243.132.6:8333", "162.243.132.58:8333", "162.248.99.164:53011", "162.248.102.117:8333", "163.158.35.110:8333", "164.15.10.189:8333", "164.40.134.171:8333", "166.230.71.67:8333", "167.160.161.199:8333", "168.103.195.250:8333", "168.144.27.112:8333", "168.158.129.29:8333", "170.75.162.86:8333", "172.90.99.174:8333", "172.245.5.156:8333", "173.23.166.47:8333", "173.32.11.194:8333", "173.34.203.76:8333", "173.171.1.52:8333", "173.175.136.13:8333", "173.230.228.139:8333", "173.247.193.70:8333", "174.49.132.28:8333", "174.52.202.72:8333", "174.53.76.87:8333", "174.109.33.28:8333", "176.28.12.169:8333", "176.35.182.214:8333", "176.36.33.113:8333", "176.36.33.121:8333", "176.58.96.173:8333", "176.121.76.84:8333", "178.62.70.16:8333", "178.62.111.26:8333", "178.76.169.59:8333", "178.79.131.32:8333", "178.162.199.216:8333", "178.175.134.35:8333", "178.248.111.4:8333", "178.254.1.170:8333", "178.254.34.161:8333", "179.43.143.120:8333", "179.208.156.198:8333", "180.200.128.58:8333", "183.78.169.108:8333", "183.96.96.152:8333", "184.68.2.46:8333", "184.73.160.160:8333", "184.94.227.58:8333", "184.152.68.163:8333", "185.7.35.114:8333", "185.28.76.179:8333", "185.31.160.202:8333", "185.45.192.129:8333", "185.66.140.15:8333", "186.2.167.23:8333", "186.220.101.142:8333", "188.26.5.33:8333", "188.75.136.146:8333", "188.120.194.140:8333", "188.121.5.150:8333", "188.138.0.114:8333", "188.138.33.239:8333", "188.166.0.82:8333", "188.182.108.129:8333", "188.191.97.208:8333", "188.226.198.102:8001", "190.10.9.217:8333", "190.75.143.144:8333", "190.139.102.146:8333", "191.237.64.28:8333", "192.3.131.61:8333", "192.99.225.3:8333", "192.110.160.122:8333", "192.146.137.1:8333", "192.183.198.204:8333", "192.203.228.71:8333", "193.0.109.3:8333", "193.12.238.204:8333", "193.91.200.85:8333", "193.234.225.156:8333", "194.6.233.38:8333", "194.63.143.136:8333", "194.126.100.246:8333", "195.134.99.195:8333", "195.159.111.98:8333", "195.159.226.139:8333", "195.197.175.190:8333", "198.48.199.108:8333", "198.57.208.134:8333", "198.57.210.27:8333", "198.62.109.223:8333", "198.167.140.8:8333", "198.167.140.18:8333", "199.91.173.234:8333", "199.127.226.245:8333", "199.180.134.116:8333", "200.7.96.99:8333", "201.160.106.86:8333", "202.55.87.45:8333", "202.60.68.242:8333", "202.60.69.232:8333", "202.124.109.103:8333", "203.30.197.77:8333", "203.88.160.43:8333", "203.151.140.14:8333", "203.219.14.204:8333", "205.147.40.62:8333", "207.235.39.214:8333", "207.244.73.8:8333", "208.12.64.225:8333", "208.76.200.200:8333", "209.40.96.121:8333", "209.126.107.176:8333", "209.141.40.149:8333", "209.190.75.59:8333", "209.208.111.142:8333", "210.54.34.164:8333", "211.72.66.229:8333", "212.51.144.42:8333", "212.112.33.157:8333", "212.116.72.63:8333", "212.126.14.122:8333", "213.66.205.194:8333", "213.111.196.21:8333", "213.122.107.102:8333", "213.136.75.175:8333", "213.155.7.24:8333", "213.163.64.31:8333", "213.163.64.208:8333", "213.165.86.136:8333", "213.184.8.22:8333", "216.15.78.182:8333", "216.55.143.154:8333", "216.115.235.32:8333", "216.126.226.166:8333", "216.145.67.87:8333", "216.169.141.169:8333", "216.249.92.230:8333", "216.250.138.230:8333", "217.20.171.43:8333", "217.23.2.71:8333", "217.23.2.242:8333", "217.25.9.76:8333", "217.40.226.169:8333", "217.123.98.9:8333", "217.155.36.62:8333", "217.172.32.18:20993", "218.61.196.202:8333", "218.231.205.41:8333", "220.233.77.200:8333", "223.18.226.85:8333", "223.197.203.82:8333", "223.255.166.142:8333" };

            Random rand = new Random();
            TimeSpan nOneWeek = TimeSpan.FromDays(7);
            for (int i = 0; i < pnSeed.Length; i++)
            {
                // It'll only connect to one or two seed nodes because once it connects,
                // it'll get a pile of addresses with newer timestamps.                
                NetworkAddress addr = new NetworkAddress();
                // Seed nodes are given a random 'last seen time' of between one and two
                // weeks ago.
                addr.Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * nOneWeek.TotalSeconds)) - nOneWeek;
                addr.Endpoint = Utils.ParseIpEndpoint(pnSeed[i], network.DefaultPort);
                network.fixedSeeds.Add(addr);
            }

            network.MaxTimeOffsetSeconds = BitcoinMaxTimeOffsetSeconds;
            network.MaxTipAge = BitcoinDefaultMaxTipAgeInSeconds;
            network.MinTxFee = 1000;
            network.FallbackFee = 20000;
            network.MinRelayTxFee = 1000;

            // Partially obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L146
            network.checkpoints.Add(11111, new CheckpointInfo(new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d")));
            network.checkpoints.Add(33333, new CheckpointInfo(new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6")));
            network.checkpoints.Add(74000, new CheckpointInfo(new uint256("0x0000000000573993a3c9e41ce34471c079dcf5f52a0e824a81e7f953b8661a20")));
            network.checkpoints.Add(105000, new CheckpointInfo(new uint256("0x00000000000291ce28027faea320c8d2b054b2e0fe44a773f3eefb151d6bdc97")));
            network.checkpoints.Add(134444, new CheckpointInfo(new uint256("0x00000000000005b12ffd4cd315cd34ffd4a594f430ac814c91184a0d42d2b0fe")));
            network.checkpoints.Add(168000, new CheckpointInfo(new uint256("0x000000000000099e61ea72015e79632f216fe6cb33d7899acb35b75c8303b763")));
            network.checkpoints.Add(193000, new CheckpointInfo(new uint256("0x000000000000059f452a5f7340de6682a977387c17010ff6e6c3bd83ca8b1317")));
            network.checkpoints.Add(210000, new CheckpointInfo(new uint256("0x000000000000048b95347e83192f69cf0366076336c639f9b7228e9ba171342e")));
            network.checkpoints.Add(216116, new CheckpointInfo(new uint256("0x00000000000001b4f4b433e81ee46494af945cf96014816a4e2370f11b23df4e")));
            network.checkpoints.Add(225430, new CheckpointInfo(new uint256("0x00000000000001c108384350f74090433e7fcf79a606b8e797f065b130575932")));
            network.checkpoints.Add(250000, new CheckpointInfo(new uint256("0x000000000000003887df1f29024b06fc2200b55f8af8f35453d7be294df2d214")));
            network.checkpoints.Add(279000, new CheckpointInfo(new uint256("0x0000000000000001ae8c72a0b0c301f67e3afca10e819efa9041e458e9bd7e40")));
            network.checkpoints.Add(295000, new CheckpointInfo(new uint256("0x00000000000000004d9b4ef50f0f9d686fd69db2e03af35a100370c64632a983")));
            // Our own new checkpoints.
            network.checkpoints.Add(486000, new CheckpointInfo(new uint256("0x000000000000000000a2a8104d61651f76c666b70754d6e9346176385f7afa24")));
            network.checkpoints.Add(491800, new CheckpointInfo(new uint256("0x000000000000000000d80de1f855902b50941bc3a3d0f71064d9613fd3943dc4")));

            NetworksContainer.TryAdd("mainnet", network);
            NetworksContainer.TryAdd(network.Name.ToLowerInvariant(), network);

            return network;
        }

        private static Network InitTest()
        {
            Network network = new Network
            {
                Name = "TestNet",
                RootFolderName = BitcoinRootFolderName,
                DefaultConfigFilename = BitcoinDefaultConfigFilename
            };

            network.consensus.SubsidyHalvingInterval = 210000;
            network.consensus.MajorityEnforceBlockUpgrade = 51;
            network.consensus.MajorityRejectBlockOutdated = 75;
            network.consensus.MajorityWindow = 100;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP34] = 21111;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP65] = 581885;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP66] = 330776;
            network.consensus.BIP34Hash = new uint256("0x0000000023b3a96d3484e5abb3755c413e7d41500f8e2a5c3f0dd01299cd8ef8");
            network.consensus.PowLimit = new Target(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            network.consensus.MinimumChainWork = new uint256("0x0000000000000000000000000000000000000000000000198b4def2baa9338d6");
            network.consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.consensus.PowAllowMinDifficultyBlocks = true;
            network.consensus.PowNoRetargeting = false;
            network.consensus.RuleChangeActivationThreshold = 1512; // 75% for testchains
            network.consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing

            network.consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
            network.consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1456790400, 1493596800);
            network.consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 1462060800, 1493596800);

            network.consensus.CoinType = 1;

            network.consensus.DefaultAssumeValid = new uint256("0x000000000000015682a21fc3b1e5420435678cba99cace2b07fe69b668467651"); // 1292762

            network.magic = 0x0709110B;

            network.alertPubKeyArray = DataEncoders.Encoders.Hex.DecodeData("04302390343f91cc401d56d68b123028bf52e5fca1939df127f63c6467cdf9c8e2c14b61104cf817d0b780da337893ecc4aaff1309e536162dabbdb45200ca2b0a");
            network.DefaultPort = 18333;
            network.RPCPort = 18332;

            // Modify the testnet genesis block so the timestamp is valid for a later start.
            network.genesis = CreateGenesisBlock(network.consensus.ConsensusFactory, 1296688602, 414098458, 0x1d00ffff, 1, Money.Coins(50m));
            network.consensus.HashGenesisBlock = network.genesis.GetHash();

            Assert(network.consensus.HashGenesisBlock == uint256.Parse("0x000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"));

            network.fixedSeeds.Clear();
            network.seeds.Clear();
            network.seeds.Add(new DNSSeedData("bitcoin.petertodd.org", "testnet-seed.bitcoin.petertodd.org"));
            network.seeds.Add(new DNSSeedData("bluematt.me", "testnet-seed.bluematt.me"));
            network.seeds.Add(new DNSSeedData("bitcoin.schildbach.de", "testnet-seed.bitcoin.schildbach.de"));

            network.base58Prefixes = Network.Main.base58Prefixes.ToArray();
            network.base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            network.base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            network.base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            network.base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            network.base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            network.base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            network.base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };
            network.base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("tb");
            network.bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            network.MaxTimeOffsetSeconds = BitcoinMaxTimeOffsetSeconds;
            network.MaxTipAge = BitcoinDefaultMaxTipAgeInSeconds;
            network.MinTxFee = 1000;
            network.FallbackFee = 20000;
            network.MinRelayTxFee = 1000;

            // Partially obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L246
            network.checkpoints.Add(546, new CheckpointInfo(new uint256("000000002a936ca763904c3c35fce2f3556c559c0214345d31b1bcebf76acb70")));
            // Our own new checkpoints.
            network.checkpoints.Add(1210000, new CheckpointInfo(new uint256("00000000461201277cf8c635fc10d042d6f0a7eaa57f6c9e8c099b9e0dbc46dc")));

            NetworksContainer.TryAdd("test", network);
            NetworksContainer.TryAdd(network.Name.ToLowerInvariant(), network);

            return network;
        }

        private static Network InitReg()
        {
            Network network = new Network
            {
                Name = "RegTest",
                RootFolderName = BitcoinRootFolderName,
                DefaultConfigFilename = BitcoinDefaultConfigFilename
            };

            network.consensus.SubsidyHalvingInterval = 150;
            network.consensus.MajorityEnforceBlockUpgrade = 750;
            network.consensus.MajorityRejectBlockOutdated = 950;
            network.consensus.MajorityWindow = 1000;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP34] = 100000000;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP65] = 100000000;
            network.consensus.BuriedDeployments[BuriedDeployments.BIP66] = 100000000;
            network.consensus.BIP34Hash = new uint256();
            network.consensus.PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            network.consensus.MinimumChainWork = uint256.Zero;
            network.consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            network.consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            network.consensus.PowAllowMinDifficultyBlocks = true;
            network.consensus.PowNoRetargeting = true;
            network.consensus.RuleChangeActivationThreshold = 108;
            network.consensus.MinerConfirmationWindow = 144;

            network.magic = 0xDAB5BFFA;

            network.consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 0, 999999999);
            network.consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 0, 999999999);
            network.consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999);

            network.genesis = CreateGenesisBlock(network.consensus.ConsensusFactory, 1296688602, 2, 0x207fffff, 1, Money.Coins(50m));
            network.consensus.HashGenesisBlock = network.genesis.GetHash();
            network.DefaultPort = 18444;
            network.RPCPort = 18332;

            network.consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

            Assert(network.consensus.HashGenesisBlock == uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"));

            network.seeds.Clear();  // Regtest mode doesn't have any DNS seeds.
            network.base58Prefixes = Network.TestNet.base58Prefixes.ToArray();
            network.base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            network.base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            network.base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            network.base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            network.base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            network.base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            var encoder = new Bech32Encoder("tb");
            network.bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            network.bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            network.MaxTimeOffsetSeconds = BitcoinMaxTimeOffsetSeconds;
            network.MaxTipAge = BitcoinDefaultMaxTipAgeInSeconds;
            network.MinTxFee = 1000;
            network.FallbackFee = 20000;
            network.MinRelayTxFee = 1000;

            NetworksContainer.TryAdd("reg", network);
            NetworksContainer.TryAdd(network.Name.ToLowerInvariant(), network);

            return network;
        }

        private static Network InitStratisMain()
        {
            var consensus = new Consensus();

            consensus.NetworkOptions = new NetworkOptions();

            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 227931;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 388381;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 363725;
            consensus.BIP34Hash = new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8");
            consensus.PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.PowAllowMinDifficultyBlocks = false;
            consensus.PowNoRetargeting = false;
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing

            consensus.BIP9Deployments[BIP9Deployments.TestDummy] = new BIP9DeploymentsParameters(28, 1199145601, 1230767999);
            consensus.BIP9Deployments[BIP9Deployments.CSV] = new BIP9DeploymentsParameters(0, 1462060800, 1493596800);
            consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, 0);

            consensus.LastPOWBlock = 12500;
            consensus.IsProofOfStake = true;
            consensus.ConsensusFactory = new PosConsensusFactory() { Consensus = consensus };

            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));

            consensus.CoinType = 105;

            consensus.DefaultAssumeValid = new uint256("0x55a8205ae4bbf18f4d238c43f43005bd66e0b1f679b39e2c5c62cf6903693a5e"); // 795970

            Block genesis = CreateStratisGenesisBlock(consensus.ConsensusFactory, 1470467000, 1831645, 0x1e0fffff, 1, Money.Zero);
            consensus.HashGenesisBlock = genesis.GetHash();

            var checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff"), new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e")) }, // Premine
                { 50, new CheckpointInfo(new uint256("0x0353b43f4ce80bf24578e7c0141d90d7962fb3a4b4b4e5a17925ca95e943b816"), new uint256("0x7c2af3b10d13f9d2bc6063baaf7f0860d90d870c994378144f9bf85d0d555061")) },
                { 100, new CheckpointInfo(new uint256("0x688468a8aa48cd1c2197e42e7d8acd42760b7e2ac4bcab9d18ac149a673e16f6"), new uint256("0xcf2b1e9e76aaa3d96f255783eb2d907bf6ccb9c1deeb3617149278f0e4a1ab1b")) },
                { 150, new CheckpointInfo(new uint256("0xe4ae9663519abec15e28f68bdb2cb89a739aee22f53d1573048d69141db6ee5d"), new uint256("0xa6c17173e958dc716cc0892ce33dad8bc327963d78a16c436264ceae43d584ce")) },
                { 127500, new CheckpointInfo(new uint256("0x4773ca7512489df22de03aa03938412fab5b46154b05df004b97bcbeaa184078"), new uint256("0x619743c02ebaff06b90fcc5c60c52dba8aa3fdb6ba3800aae697cbb3c5483f17")) },
                { 128943, new CheckpointInfo(new uint256("0x36bcaa27a53d3adf22b2064150a297adb02ac39c24263a5ceb73856832d49679"), new uint256("0xa3a6fd04e41fcaae411a3990aaabcf5e086d2d06c72c849182b27b4de8c2c42a")) },
                { 136601, new CheckpointInfo(new uint256("0xf5c5210c55ff1ef9c04715420a82728e1647f3473e31dc478b3745a97b4a6d10"), new uint256("0x42058fabe21f7b118a9e358eaf9ef574dadefd024244899e71f2f6d618161e16")) }, // Hardfork to V2 - Drifting Bug Fix
                { 170000, new CheckpointInfo(new uint256("0x22b10952e0cf7e85bfc81c38f1490708f195bff34d2951d193cc20e9ca1fc9d5"), new uint256("0xa4942a6c99cba397cf2b18e4b912930fe1e64a7413c3d97c5a926c2af9073091")) },
                { 200000, new CheckpointInfo(new uint256("0x2391dd493be5d0ff0ef57c3b08c73eefeecc2701b80f983054bb262f7a146989"), new uint256("0x253152d129e82c30c584197deb6833502eff3ec2f30014008f75842d7bb48453")) },
                { 250000, new CheckpointInfo(new uint256("0x681c70fab7c1527246138f0cf937f0eb013838b929fbe9a831af02a60fc4bf55"), new uint256("0x24eed95e00c90618aa9d137d2ee273267285c444c9cde62a25a3e880c98a3685")) },
                { 300000, new CheckpointInfo(new uint256("0xd10ca8c2f065a49ae566c7c9d7a2030f4b8b7f71e4c6fc6b2a02509f94cdcd44"), new uint256("0x39c4dd765b49652935524248b4de4ccb604df086d0723bcd81faf5d1c2489304")) },
                { 350000, new CheckpointInfo(new uint256("0xe2b76d1a068c4342f91db7b89b66e0f2146d3a4706c21f3a262737bb7339253a"), new uint256("0xd1dd94985eaaa028c893687a7ddf89143dcf0176918f958c2d01f33d64910399")) },
                { 390000, new CheckpointInfo(new uint256("0x4682737abc2a3257fdf4c3c119deb09cbac75981969e2ffa998b4f76b7c657bb"), new uint256("0xd84b204ee94499ff65262328a428851fb4f4d2741e928cdd088fdf1deb5413b8")) },
                { 394000, new CheckpointInfo(new uint256("0x42857fa2bc15d45cdcaae83411f755b95985da1cb464ee23f6d40936df523e9f"), new uint256("0x2314b336906a2ed2a39cbdf6fc0622530709c62dbb3a3729de17154fc9d1a7c4")) },
                { 528000, new CheckpointInfo(new uint256("0x7aff2c48b398446595d01e27b5cd898087cec63f94ff73f9ad695c6c9bcee33a"), new uint256("0x3bdc865661390c7681b078e52ed3ad3c53ec7cff97b8c45b74abed3ace289fcc")) },
                { 576000, new CheckpointInfo(new uint256("0xe705476b940e332098d1d5b475d7977312ff8c08cbc8256ce46a3e2c6d5408b8"), new uint256("0x10e31bb5e245ea19650280cfd3ac1a76259fa0002d02e861d2ab5df290534b56")) },
            };

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x70;
            messageStart[1] = 0x35;
            messageStart[2] = 0x22;
            messageStart[3] = 0x05;
            var magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570; 

            Assert(consensus.HashGenesisBlock == uint256.Parse("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"));
            Assert(genesis.Header.HashMerkleRoot == uint256.Parse("0x65a26bc20b0351aebf05829daefa8f7db2f800623439f3c114257c91447f1518"));

            NetworkBuilder builder = new NetworkBuilder()
                    .SetName("StratisMain")
                    .SetRootFolderName(StratisRootFolderName)
                    .SetDefaultConfigFilename(StratisDefaultConfigFilename)
                    .SetConsensus(consensus)
                    .SetCheckpoints(checkpoints)
                    .SetMagic(magic)
                    .SetGenesis(genesis)
                    .SetPort(16178)
                    .SetRPCPort(16174)
                    .SetTxFees(10000, 60000, 10000)
                    .SetMaxTimeOffsetSeconds(StratisMaxTimeOffsetSeconds)
                    .SetMaxTipAge(StratisDefaultMaxTipAgeInSeconds)

                    .AddDNSSeeds(new[]
                    {
                        new DNSSeedData("seednode1.stratisplatform.com", "seednode1.stratisplatform.com"),
                        new DNSSeedData("seednode2.stratis.cloud", "seednode2.stratis.cloud"),
                        new DNSSeedData("seednode3.stratisplatform.com", "seednode3.stratisplatform.com"),
                        new DNSSeedData("seednode4.stratis.cloud", "seednode4.stratis.cloud")
                    })

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
                    .SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, "bc");

            var seed = new[] { "101.200.198.155", "103.24.76.21", "104.172.24.79" };
            var fixedSeeds = new List<NetworkAddress>();
            // Convert the pnSeeds array into usable address objects.
            Random rand = new Random();
            TimeSpan oneWeek = TimeSpan.FromDays(7);
            for (int i = 0; i < seed.Length; i++)
            {
                // It'll only connect to one or two seed nodes because once it connects,
                // it'll get a pile of addresses with newer timestamps.                
                NetworkAddress addr = new NetworkAddress();
                // Seed nodes are given a random 'last seen time' of between one and two
                // weeks ago.
                addr.Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * oneWeek.TotalSeconds)) - oneWeek;
                addr.Endpoint = Utils.ParseIpEndpoint(seed[i], builder.Port);
                fixedSeeds.Add(addr);
            }

            builder.AddSeeds(fixedSeeds);
            return builder.BuildAndRegister();
        }

        private static Network InitStratisTest()
        {
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

            Block genesis = Network.StratisMain.GetGenesis();
            genesis.Header.Time = 1493909211;
            genesis.Header.Nonce = 2433759;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Assert(consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));

            consensus.DefaultAssumeValid = new uint256("0x98fa6ef0bca5b431f15fd79dc6f879dc45b83ed4b1bbe933a383ef438321958e"); // 372652

            var checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0x56959b1c8498631fb0ca5fe7bd83319dccdc6ac003dccb3171f39f553ecfa2f2"), new uint256("0x13f4c27ca813aefe2d9018077f8efeb3766796b9144fcc4cd51803bf4376ab02")) },
                { 50000, new CheckpointInfo(new uint256("0xb42c18eacf8fb5ed94eac31943bd364451d88da0fd44cc49616ffea34d530ad4"), new uint256("0x824934ddc5f935e854ac59ae7f5ed25f2d29a7c3914cac851f3eddb4baf96d78")) },
                { 100000, new CheckpointInfo(new uint256("0xf9e2f7561ee4b92d3bde400d251363a0e8924204c326da7f4ad9ccc8863aad79"), new uint256("0xdef8d92d20becc71f662ee1c32252aca129f1bf4744026b116d45d9bfe67e9fb")) },
                { 115000, new CheckpointInfo(new uint256("0x8496c77060c8a2b5c9a888ade991f25aa33c232b4413594d556daf9043fad400"), new uint256("0x1886430484a9a36b56a7eb8bd25e9ebe4fc8eec8f9a84f5073f71e08f2feac90")) },
                { 163000, new CheckpointInfo(new uint256("0x4e44a9e0119a2e7cbf15e570a3c649a5605baa601d953a465b5ebd1c1982212a"), new uint256("0x0646fc7db8f3426eb209e1228c7d82724faa46a060f5bbbd546683ef30be245c")) },
            };

            NetworkBuilder builder = new NetworkBuilder()
                    .SetName("StratisTest")
                    .SetRootFolderName(StratisRootFolderName)
                    .SetDefaultConfigFilename(StratisDefaultConfigFilename)
                    .SetConsensus(consensus)
                    .SetCheckpoints(checkpoints)
                    .SetMagic(magic)
                    .SetGenesis(genesis)
                    .SetPort(26178)
                    .SetRPCPort(26174)
                    .SetMaxTimeOffsetSeconds(StratisMaxTimeOffsetSeconds)
                    .SetMaxTipAge(StratisDefaultMaxTipAgeInSeconds)
                    .SetTxFees(10000, 60000, 10000)
                    .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
                    .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                    .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                    .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                    .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                    .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                    .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })

                    .AddDNSSeeds(new[]
                    {
                        new DNSSeedData("testnet1.stratisplatform.com", "testnet1.stratisplatform.com"),
                        new DNSSeedData("testnet2.stratisplatform.com", "testnet2.stratisplatform.com"),
                        new DNSSeedData("testnet3.stratisplatform.com", "testnet3.stratisplatform.com"),
                        new DNSSeedData("testnet4.stratisplatform.com", "testnet4.stratisplatform.com")
                    });

            builder.AddSeeds(new[]
            {
                new NetworkAddress(IPAddress.Parse("51.140.231.125"), builder.Port), // danger cloud node
                new NetworkAddress(IPAddress.Parse("13.70.81.5"), 3389), // beard cloud node  
                new NetworkAddress(IPAddress.Parse("191.235.85.131"), 3389), // fassa cloud node  
                new NetworkAddress(IPAddress.Parse("52.232.58.52"), 26178), // neurosploit public node
            });

            return builder.BuildAndRegister();
        }

        private static Network InitStratisRegTest()
        {
            var net = Network.GetNetwork("StratisRegTest");
            if (net != null)
                return net;

            var consensus = Network.StratisTest.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            consensus.PowAllowMinDifficultyBlocks = true;
            consensus.PowNoRetargeting = true;

            var messageStart = new byte[4];
            messageStart[0] = 0xcd;
            messageStart[1] = 0xf2;
            messageStart[2] = 0xc0;
            messageStart[3] = 0xef;
            var magic = BitConverter.ToUInt32(messageStart, 0);

            Block genesis = Network.StratisMain.GetGenesis();
            genesis.Header.Time = 1494909211;
            genesis.Header.Nonce = 2433759;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Assert(consensus.HashGenesisBlock == uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"));

            consensus.DefaultAssumeValid = null; // turn off assumevalid for regtest.

            NetworkBuilder builder = new NetworkBuilder()
                .SetName("StratisRegTest")
                .SetRootFolderName(StratisRootFolderName)
                .SetDefaultConfigFilename(StratisDefaultConfigFilename)
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(18444)
                .SetRPCPort(18442)
                .SetMaxTimeOffsetSeconds(StratisMaxTimeOffsetSeconds)
                .SetMaxTipAge(StratisDefaultMaxTipAgeInSeconds)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) });

            return builder.BuildAndRegister();
        }

        private static Block CreateGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "The Times 03/Jan/2009 Chancellor on brink of second bailout for banks";
            Script genesisOutputScript = new Script(Op.GetPushOp(Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f")), OpcodeType.OP_CHECKSIG);
            return CreateGenesisBlock(consensusFactory, pszTimestamp, genesisOutputScript, nTime, nNonce, nBits, nVersion, genesisReward);
        }

        private static Block CreateGenesisBlock(ConsensusFactory consensusFactory, string pszTimestamp, Script genesisOutputScript, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
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

        private static Block CreateStratisGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "http://www.theonion.com/article/olympics-head-priestess-slits-throat-official-rio--53466";
            return CreateStratisGenesisBlock(consensusFactory, pszTimestamp, nTime, nNonce, nBits, nVersion, genesisReward);
        }

        private static Block CreateStratisGenesisBlock(ConsensusFactory consensusFactory, string pszTimestamp, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
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
    }
}
