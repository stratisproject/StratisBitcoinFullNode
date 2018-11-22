using System;
using System.Threading;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Sidechains.Networks
{
    [Obsolete("Please use FederatedPegNetwork")]
    public class ApexNetwork
    {
        public const string ChainName = "Apex";
        public const string MainNetworkName = ChainName + "Main";
        public const string TestNetworkName = ChainName + "Test";
        public const string RegTestNetworkName = ChainName + "RegTest";

        public static NetworksSelector NetworksSelector
        {
            get
            {
                return new NetworksSelector(() => new ApexMain(), () => new ApexTest(), () => new ApexRegTest());
            }
        }

        public static Block CreateGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
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
    }
}
