using System;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Networks.Apex
{
    public class ApexNetwork
    {
        public const string ChainName = "Apex";

        public const string MainNetworkName = "ApexMain";
        public const string TestNetworkName = "ApexTest";
        public const string RegTestNetworkName = "ApexRegTest";

        public static Network Main => Network.GetNetwork(MainNetworkName) ?? Network.Register(new ApexMain());

        public static Network Test => Network.GetNetwork(TestNetworkName) ?? Network.Register(new ApexTest());

        public static Network RegTest => Network.GetNetwork(RegTestNetworkName) ?? Network.Register(new ApexRegTest());

        public static Block CreateApexGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "https://www.coindesk.com/apple-co-founder-backs-dorsey-bitcoin-become-webs-currency/";

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

        private static void Assert(bool condition)
        {
            // TODO: use Guard when this moves to the FN.
            if (!condition)
            {
                throw new InvalidOperationException("Invalid network");
            }
        }
    }
}
