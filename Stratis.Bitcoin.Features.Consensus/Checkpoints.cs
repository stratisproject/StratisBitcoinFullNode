using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Interface of block header hash checkpoint provider.
    /// </summary>
    public interface ICheckpoints
    {
        /// <summary>
        /// Obtains a height of the last checkpointed block.
        /// </summary>
        /// <returns>Height of the last checkpointed block, or 0 if no checkpoint is available.</returns>
        int GetLastCheckpointHeight();

        /// <summary>
        /// Checks if a block header hash at specific height is in violation with the hardcoded checkpoints.
        /// </summary>
        /// <param name="height">Height of the block.</param>
        /// <param name="hash">Block header hash to check.</param>
        /// <returns><c>true</c> if either there is no checkpoint for the given height, or if the checkpointed block header hash equals to the checked <paramref name="hash"/>.
        /// <c>false</c> if there is a checkpoint for the given <paramref name="height"/>, but the checkpointed block header hash is not the same as the checked <paramref name="hash"/>.</returns>
        bool CheckHardened(int height, uint256 hash);
    }

    /// <summary>
    /// Checkpoints is a mechanism on how to avoid validation of historic blocks for which there 
    /// already is a consensus on the network. This allows speeding up IBD, especially on POS networks.
    /// </summary>
    /// <remarks>
    /// From https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L66 :
    /// What makes a good checkpoint block? It is surrounded by blocks with reasonable timestamps
    //  (no blocks before with a timestamp after, none after with timestamp before). It also contains 
    //  no strange transactions.
    /// </remarks>
    public class Checkpoints : ICheckpoints
    {
        /// <summary>List of selected checkpoints for STRAT mainnet.</summary>
        private static Dictionary<int, uint256> stratisMainnetCheckpoints = new Dictionary<int, uint256>()
        {
            { 0, new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af") },
            { 2, new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff") }, // Premine
            { 50, new uint256("0x0353b43f4ce80bf24578e7c0141d90d7962fb3a4b4b4e5a17925ca95e943b816") },
            { 100, new uint256("0x688468a8aa48cd1c2197e42e7d8acd42760b7e2ac4bcab9d18ac149a673e16f6") },
            { 150, new uint256("0xe4ae9663519abec15e28f68bdb2cb89a739aee22f53d1573048d69141db6ee5d") },
            { 127500, new uint256("0x4773ca7512489df22de03aa03938412fab5b46154b05df004b97bcbeaa184078") },
            { 128943, new uint256("0x36bcaa27a53d3adf22b2064150a297adb02ac39c24263a5ceb73856832d49679") },
            { 136601, new uint256("0xf5c5210c55ff1ef9c04715420a82728e1647f3473e31dc478b3745a97b4a6d10") }, // Hardfork to V2 - Drifting Bug Fix
            { 170000, new uint256("0x22b10952e0cf7e85bfc81c38f1490708f195bff34d2951d193cc20e9ca1fc9d5") },
            { 200000, new uint256("0x2391dd493be5d0ff0ef57c3b08c73eefeecc2701b80f983054bb262f7a146989") },
            { 250000, new uint256("0x681c70fab7c1527246138f0cf937f0eb013838b929fbe9a831af02a60fc4bf55") },
            { 300000, new uint256("0xd10ca8c2f065a49ae566c7c9d7a2030f4b8b7f71e4c6fc6b2a02509f94cdcd44") },
            { 350000, new uint256("0xe2b76d1a068c4342f91db7b89b66e0f2146d3a4706c21f3a262737bb7339253a") },
            { 390000, new uint256("0x4682737abc2a3257fdf4c3c119deb09cbac75981969e2ffa998b4f76b7c657bb") },
            { 394000, new uint256("0x42857fa2bc15d45cdcaae83411f755b95985da1cb464ee23f6d40936df523e9f") },
            { 528000, new uint256("0x7aff2c48b398446595d01e27b5cd898087cec63f94ff73f9ad695c6c9bcee33a") },
        };

        /// <summary>List of selected checkpoints for STRAT testnet.</summary>
        private static Dictionary<int, uint256> stratisTestnetCheckpoints = new Dictionary<int, uint256>()
        {
            { 0, new uint256("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9") },
            { 2, new uint256("0x56959b1c8498631fb0ca5fe7bd83319dccdc6ac003dccb3171f39f553ecfa2f2") },
            { 50000, new uint256("0xb42c18eacf8fb5ed94eac31943bd364451d88da0fd44cc49616ffea34d530ad4") },
            { 100000, new uint256("0xf9e2f7561ee4b92d3bde400d251363a0e8924204c326da7f4ad9ccc8863aad79") },
        };

        /// <summary>List of selected checkpoints for Bitcoin mainnet.</summary>
        /// <remarks>Partially obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L146 .</remarks>
        private static Dictionary<int, uint256> bitcoinMainnetCheckpoints = new Dictionary<int, uint256>()
        {
            { 11111, new uint256("0x0000000069e244f73d78e8fd29ba2fd2ed618bd6fa2ee92559f542fdb26e7c1d") },
            { 33333, new uint256("0x000000002dd5588a74784eaa7ab0507a18ad16a236e7b1ce69f00d7ddfb5d0a6") },
            { 74000, new uint256("0x0000000000573993a3c9e41ce34471c079dcf5f52a0e824a81e7f953b8661a20") },
            { 105000, new uint256("0x00000000000291ce28027faea320c8d2b054b2e0fe44a773f3eefb151d6bdc97") },
            { 134444, new uint256("0x00000000000005b12ffd4cd315cd34ffd4a594f430ac814c91184a0d42d2b0fe") },
            { 168000, new uint256("0x000000000000099e61ea72015e79632f216fe6cb33d7899acb35b75c8303b763") },
            { 193000, new uint256("0x000000000000059f452a5f7340de6682a977387c17010ff6e6c3bd83ca8b1317") },
            { 210000, new uint256("0x000000000000048b95347e83192f69cf0366076336c639f9b7228e9ba171342e") },
            { 216116, new uint256("0x00000000000001b4f4b433e81ee46494af945cf96014816a4e2370f11b23df4e") },
            { 225430, new uint256("0x00000000000001c108384350f74090433e7fcf79a606b8e797f065b130575932") },
            { 250000, new uint256("0x000000000000003887df1f29024b06fc2200b55f8af8f35453d7be294df2d214") },
            { 279000, new uint256("0x0000000000000001ae8c72a0b0c301f67e3afca10e819efa9041e458e9bd7e40") },
            { 295000, new uint256("0x00000000000000004d9b4ef50f0f9d686fd69db2e03af35a100370c64632a983") },
            // Our own new checkpoints.
            { 486000, new uint256("0x000000000000000000a2a8104d61651f76c666b70754d6e9346176385f7afa24") },
        };

        /// <summary>List of selected checkpoints for Bitcoin testnet.</summary>
        /// <remarks>Obtained from https://github.com/bitcoin/bitcoin/blob/b1973d6181eacfaaf45effb67e0c449ea3a436b8/src/chainparams.cpp#L246 .</remarks>
        private static Dictionary<int, uint256> bitcoinTestnetCheckpoints = new Dictionary<int, uint256>()
        {
            { 546, new uint256("000000002a936ca763904c3c35fce2f3556c559c0214345d31b1bcebf76acb70") },
        };

        /// <summary>Checkpoints for the specific instance of the class and its network.</summary>
        private readonly Dictionary<int, uint256> checkpoints;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet/stratis test/main.</param>
        public Checkpoints(Network network)
        {
            if (network.Equals(Network.Main)) this.checkpoints = bitcoinMainnetCheckpoints;
            else if (network.Equals(Network.TestNet)) this.checkpoints = bitcoinTestnetCheckpoints;
            else if (network.Equals(Network.StratisMain)) this.checkpoints = stratisMainnetCheckpoints;
            else if (network.Equals(Network.StratisTest)) this.checkpoints = stratisTestnetCheckpoints;
            else this.checkpoints = new Dictionary<int, uint256>();
        }

        /// <inheritdoc />
        public int GetLastCheckpointHeight()
        {
            return this.checkpoints.Count > 0 ? this.checkpoints.Keys.Last() : 0;
        }

        /// <inheritdoc />
        public bool CheckHardened(int height, uint256 hash)
        {
            uint256 checkpointHash;
            if (!this.checkpoints.TryGetValue(height, out checkpointHash)) return true;

            return checkpointHash.Equals(hash);
        }
    }
}
