using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.SmartContracts.PoA;

namespace Stratis.Sidechains.Networks
{
    public static class FederatedPegNetwork
    {
        public static NetworksSelector NetworksSelector
        {
            get
            {
                return new NetworksSelector(() => new FederatedPegMain(), () => new FederatedPegTest(), () => new FederatedPegRegTest());
            }
        }

        public static Block CreateGenesis(SmartContractPoAConsensusFactory consensusFactory, uint genesisTime, uint nonce, uint bits, int version, Money reward, string coinbaseText)
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