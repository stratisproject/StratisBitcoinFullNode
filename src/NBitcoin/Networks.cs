using NBitcoin.DataEncoders;
using NBitcoin.Networks;

namespace NBitcoin
{
    public partial class Network
    {
        /// <summary> Bitcoin maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int BitcoinMaxTimeOffsetSeconds = 70 * 60;

        /// <summary> Bitcoin default value for the maximum tip age in seconds to consider the node in initial block download (24 hours). </summary>
        public const int BitcoinDefaultMaxTipAgeInSeconds = 24 * 60 * 60;

        /// <summary> The name of the root folder containing the different Bitcoin blockchains (Main, TestNet, RegTest). </summary>
        public const string BitcoinRootFolderName = "bitcoin";

        /// <summary> The default name used for the Bitcoin configuration file. </summary>
        public const string BitcoinDefaultConfigFilename = "bitcoin.conf";

        public static Network Main => GetNetwork("Main") ?? Register(new BitcoinMain());

        public static Network TestNet => GetNetwork("TestNet") ?? Register(new BitcoinTest());

        public static Network RegTest => GetNetwork("RegTest") ?? Register(new BitcoinRegTest());

        public static Network StratisMain => GetNetwork("StratisMain") ?? Register(new StratisMain());

        public static Network StratisTest => GetNetwork("StratisTest") ?? Register(new StratisTest());

        public static Network StratisRegTest => GetNetwork("StratisRegTest") ?? Register(new StratisRegTest());

        protected static Block CreateBitcoinGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
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
