using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin.NetworkDefinitions
{
    public class CityTest : CityMain
    {
        public CityTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0x67;
            messageStart[1] = 0x84;
            messageStart[2] = 0x89;
            messageStart[3] = 0x26;
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0x26898467;

            this.Name = "CityTest";
            this.Magic = magic;
            this.DefaultPort = 24333;
            this.RPCPort = 24334;
            this.CoinTicker = "TCITY";
            
            var consensus = new Consensus();
            consensus.SubsidyHalvingInterval = 210000;
            consensus.MajorityEnforceBlockUpgrade = 750;
            consensus.MajorityRejectBlockOutdated = 950;
            consensus.MajorityWindow = 1000;
            consensus.BuriedDeployments[BuriedDeployments.BIP34] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP65] = 0;
            consensus.BuriedDeployments[BuriedDeployments.BIP66] = 0;
            consensus.BIP34Hash = new uint256("0x000fd5ab3150ba1f57dbbfb449b67f4d4a30a634d997b269eccb0a48dd7cd3d9");
            consensus.PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60); // two weeks
            consensus.PowTargetSpacing = TimeSpan.FromSeconds(10 * 60);
            consensus.PowAllowMinDifficultyBlocks = false;
            consensus.PowNoRetargeting = false;
            consensus.RuleChangeActivationThreshold = 1916; // 95% of 2016
            consensus.MinerConfirmationWindow = 2016; // nPowTargetTimespan / nPowTargetSpacing
            consensus.LastPOWBlock = 125000;
            consensus.IsProofOfStake = true;
            consensus.ConsensusFactory = new PosConsensusFactory();
            consensus.ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false));
            consensus.CoinType = 4535;
            consensus.PremineReward = Money.Coins(13736000000);
            consensus.PremineHeight = 2;
            consensus.ProofOfWorkReward = Money.Coins(1); // Produced up until last POW block.
            consensus.ProofOfStakeReward = Money.Coins(100); // 52 560 000 a year.
            consensus.MaxReorgLength = 500;
            consensus.MaxMoney = long.MaxValue;

            // CityTest consensus differences
            consensus.PowLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));
            consensus.DefaultAssumeValid = new uint256("0x000fd5ab3150ba1f57dbbfb449b67f4d4a30a634d997b269eccb0a48dd7cd3d9"); // 0 TODO: Update after mining.
            consensus.CoinbaseMaturity = 10;

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (66) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (66 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                //{ 0, new CheckpointInfo(new uint256("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                //{ 2, new CheckpointInfo(new uint256("0x56959b1c8498631fb0ca5fe7bd83319dccdc6ac003dccb3171f39f553ecfa2f2"), new uint256("0x13f4c27ca813aefe2d9018077f8efeb3766796b9144fcc4cd51803bf4376ab02")) },
                //{ 50000, new CheckpointInfo(new uint256("0xb42c18eacf8fb5ed94eac31943bd364451d88da0fd44cc49616ffea34d530ad4"), new uint256("0x824934ddc5f935e854ac59ae7f5ed25f2d29a7c3914cac851f3eddb4baf96d78")) },
                //{ 100000, new CheckpointInfo(new uint256("0xf9e2f7561ee4b92d3bde400d251363a0e8924204c326da7f4ad9ccc8863aad79"), new uint256("0xdef8d92d20becc71f662ee1c32252aca129f1bf4744026b116d45d9bfe67e9fb")) },
                //{ 150000, new CheckpointInfo(new uint256("0x08b7c20a450252ddf9ce41dbeb92ecf54932beac9090dc8250e933ad3a175381"), new uint256("0xf05dad15f733ae0acbd34adc449be9429099dbee5fa9ecd8e524cf28e9153adb")) },
                //{ 200000, new CheckpointInfo(new uint256("0x8609cc873222a0573615788dc32e377b88bfd6a0015791f627d969ee3a415115"), new uint256("0xfa28c1f20a8162d133607c6a1c8997833befac3efd9076567258a7683ac181fa")) },
                //{ 250000, new CheckpointInfo(new uint256("0xdd664e15ac679a6f3b96a7176303956661998174a697ad8231f154f1e32ff4a3"), new uint256("0x19fc0fa29418f8b19cbb6557c1c79dfd0eff6779c0eaaec5d245c5cdf3c96d78")) },
                //{ 300000, new CheckpointInfo(new uint256("0x2409eb5ae72c80d5b37c77903d75a8e742a33843ab633935ce6e5264db962e23"), new uint256("0xf5ec7af55516b8e264ed280e9a5dba0180a4a9d3713351bfea275b18f3f1514e")) },
                //{ 350000, new CheckpointInfo(new uint256("0x36811041e9060f4b4c26dc20e0850dca5efaabb60618e3456992e9c0b1b2120e"), new uint256("0xbfda55ef0756bcee8485e15527a2b8ca27ca877aa09c88e363ef8d3253cdfd1c")) },
                //{ 400000, new CheckpointInfo(new uint256("0xb6abcb933d3e3590345ca5d3abb697461093313f8886568ac8ae740d223e56f6"), new uint256("0xfaf5fcebee3ec0df5155393a99da43de18b12e620fef5edb111a791ecbfaa63a")) }
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("testnode1.city-chain.org", "testnode1.city-chain.org"),
                //new DNSSeedData("testnode1.citychain.foundation", "testnode1.citychain.foundation"),
                //new DNSSeedData("testnode2.city-chain.org", "testnode2.city-chain.org"),
                //new DNSSeedData("testnode2.citychain.foundation", "testnode2.citychain.foundation")
            };

            string[] seedNodes = {
                "40.91.197.238:24333", // city-chain.org (testnode1)
                "40.91.197.238:25333", // city-chain.org (testnode1)
                "40.91.197.238:26333", // city-chain.org (testnode1)
                //"10.0.0.192:26333", // City 1 Node #1
                //"10.0.0.192:27333" // City 2 Node #1
                // As more cities are included in the City Chain, consider adding their official nodes to the seeds.
            };
            this.SeedNodes = ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            // Create the genesis block.
            this.GenesisTime = 1496275200;
            this.GenesisNonce = 14660;
            this.GenesisBits = 0x1F0FFFFF;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // 2017-08-18: "Libertarianisme er en politisk tankeretning som legger stor vekt på individuell frihet, og prosjektet er blitt omtalt over hele verden på sentrale nettsider for folk som handler med kryptovalutaer, altså penger og sikre verdipapirer som fungerer helt uten en sentral myndighet."
            // https://morgenbladet.no/aktuelt/2017/08/privatlivets-fred-i-liberstad
            string pszTimestamp = "August 18 2017, Morgenbladet, Money that work without a central authority";

            this.Consensus = consensus;

            this.Genesis = CreateCityGenesisBlock(consensus.ConsensusFactory, pszTimestamp, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            //this.Genesis.Header.Time = 1493909211;
            //this.Genesis.Header.Nonce = 2433759;
            //this.Genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = this.Genesis.GetHash();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x000fd5ab3150ba1f57dbbfb449b67f4d4a30a634d997b269eccb0a48dd7cd3d9"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0xc83d0753a8826119c898cc23828356b760f0042ff8e9d67b3a03edfce5824a74"));
        }
    }
}