using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Sidechains.Networks
{
    public static class FederatedPegNetwork
    {
        public const string ChainName = "FederatedPeg";
        public const string MainNetworkName = ChainName + "Main";
        public const string TestNetworkName = ChainName + "Test";
        public const string RegTestNetworkName = ChainName + "RegTest";

        public const string CoinSymbol = "FPG";
        public const string TestCoinSymbol = "T" + CoinSymbol;

        public static NetworksSelector NetworksSelector
        {
            get
            {
                return new NetworksSelector(() => new FederatedPegMain(), () => new FederatedPegTest(), () => new FederatedPegRegTest());
            }
        }

        public static Block CreateGenesis(ConsensusFactory consensusFactory, uint genesisTime, uint nonce, uint bits, int version, Money reward)
        {
            string timeStamp = "https://news.bitcoin.com/markets-update-cryptocurrencies-shed-billions-in-bloody-sell-off/";
            var genesisOutputScript = new Script(Op.GetPushOp(Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f")), OpcodeType.OP_CHECKSIG);

            NBitcoin.Transaction genesisTransaction = consensusFactory.CreateTransaction();
            genesisTransaction.Time = genesisTime;
            genesisTransaction.Version = 1;
            genesisTransaction.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(486604799), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)4 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(timeStamp)))
            });

            genesisTransaction.AddOutput(new TxOut()
            {
                Value = reward,
                ScriptPubKey = genesisOutputScript
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(genesisTime);
            genesis.Header.Bits = bits;
            genesis.Header.Nonce = nonce;
            genesis.Header.Version = version;
            genesis.Transactions.Add(genesisTransaction);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            //((SmartContractBlockHeader)genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");

            return genesis;
        }
    }
}