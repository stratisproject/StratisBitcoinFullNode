using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NBitcoin.Protocol;

namespace NBitcoin.Networks
{
    public class StratisTest : StratisMain
    {
        public StratisTest()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x31;
            messageStart[2] = 0x21;
            messageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(messageStart, 0); // 0x11213171;

            this.Name = "StratisTest";
            this.Magic = magic;
            this.DefaultPort = 26178;
            this.RPCPort = 26174;

            this.Consensus.PowLimit = new Target(new uint256("0000ffff00000000000000000000000000000000000000000000000000000000"));
            this.Consensus.DefaultAssumeValid = new uint256("0x98fa6ef0bca5b431f15fd79dc6f879dc45b83ed4b1bbe933a383ef438321958e"); // 372652

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0x56959b1c8498631fb0ca5fe7bd83319dccdc6ac003dccb3171f39f553ecfa2f2"), new uint256("0x13f4c27ca813aefe2d9018077f8efeb3766796b9144fcc4cd51803bf4376ab02")) },
                { 50000, new CheckpointInfo(new uint256("0xb42c18eacf8fb5ed94eac31943bd364451d88da0fd44cc49616ffea34d530ad4"), new uint256("0x824934ddc5f935e854ac59ae7f5ed25f2d29a7c3914cac851f3eddb4baf96d78")) },
                { 100000, new CheckpointInfo(new uint256("0xf9e2f7561ee4b92d3bde400d251363a0e8924204c326da7f4ad9ccc8863aad79"), new uint256("0xdef8d92d20becc71f662ee1c32252aca129f1bf4744026b116d45d9bfe67e9fb")) },
                { 115000, new CheckpointInfo(new uint256("0x8496c77060c8a2b5c9a888ade991f25aa33c232b4413594d556daf9043fad400"), new uint256("0x1886430484a9a36b56a7eb8bd25e9ebe4fc8eec8f9a84f5073f71e08f2feac90")) },
                { 163000, new CheckpointInfo(new uint256("0x4e44a9e0119a2e7cbf15e570a3c649a5605baa601d953a465b5ebd1c1982212a"), new uint256("0x0646fc7db8f3426eb209e1228c7d82724faa46a060f5bbbd546683ef30be245c")) }
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("testnet1.stratisplatform.com", "testnet1.stratisplatform.com"),
                new DNSSeedData("testnet2.stratisplatform.com", "testnet2.stratisplatform.com"),
                new DNSSeedData("testnet3.stratisplatform.com", "testnet3.stratisplatform.com"),
                new DNSSeedData("testnet4.stratisplatform.com", "testnet4.stratisplatform.com")
            };

            this.SeedNodes = new List<NetworkAddress>
            {
                new NetworkAddress(IPAddress.Parse("51.140.231.125"), this.DefaultPort), // danger cloud node
                new NetworkAddress(IPAddress.Parse("13.70.81.5"), 3389), // beard cloud node  
                new NetworkAddress(IPAddress.Parse("191.235.85.131"), 3389), // fassa cloud node  
                new NetworkAddress(IPAddress.Parse("52.232.58.52"), 26178), // neurosploit public node
            };

            // Create the genesis block.
            this.GenesisTime = 1470467000;
            this.GenesisNonce = 1831645;
            this.GenesisBits = 0x1e0fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            this.Genesis = Network.CreateStratisGenesisBlock(this.Consensus.ConsensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);
            this.Consensus.HashGenesisBlock = this.Genesis.GetHash(this.Consensus.ConsensusFactory);
            this.Genesis.Header.Time = 1493909211;
            this.Genesis.Header.Nonce = 2433759;
            this.Genesis.Header.Bits = this.Consensus.PowLimit;
            Network.Assert(this.Genesis.GetHash(this.Consensus.ConsensusFactory) == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));
        }
    }
}
