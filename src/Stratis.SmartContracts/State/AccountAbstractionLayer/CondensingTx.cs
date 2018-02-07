using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class CondensingTx
    {
        private SmartContractTransaction scTransaction;
        private IList<TransferInfo> transfers;
        private IList<StoredVin> unspents;
        private IContractStateRepository state;
        private Dictionary<uint160, ulong> txBalances;

        public CondensingTx(SmartContractTransaction scTransaction, IList<TransferInfo> transfers, IList<StoredVin> unspents, IContractStateRepository state)
        {
            this.scTransaction = scTransaction;
            this.transfers = transfers;
            this.unspents = unspents;
            this.state = state;
            this.txBalances = new Dictionary<uint160, ulong>();
        }

        public Transaction CreateCondensingTransaction()
        {
            SetupBalances();

            // create inputs from stored vins
            Transaction tx = new Transaction();
            ulong vinTotal = 0;
            foreach(StoredVin vin in this.unspents)
            {
                OutPoint outpoint = new OutPoint(vin.Hash, vin.Nvout);
                tx.AddInput(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
                vinTotal += vin.Value;
            }

            ulong sendTotal = 0;

            foreach(KeyValuePair<uint160, ulong> b in this.txBalances)
            {
                //create txout from each balance
                Script script;

                AccountState a = this.state.GetAccountState(b.Key);
                if (a != null)
                {
                    SmartContractTransaction newScTransaction = new SmartContractTransaction
                    {
                        To = b.Key,
                        OpCodeType = OpcodeType.OP_CALLCONTRACT,
                        MethodName = ""
                    };
                    script = new Script(newScTransaction.ToBytes());
                }
                else
                {
                    script = new Script(
                            OpcodeType.OP_DUP,
                            OpcodeType.OP_HASH160,
                            Op.GetPushOp(b.Key.ToBytes()),
                            OpcodeType.OP_EQUALVERIFY,
                            OpcodeType.OP_CHECKSIG
                        );
                }
                tx.AddOutput(new TxOut(new Money(b.Value), script));
                sendTotal += b.Value;
            }

            // create 'change' transaction for contract
            ulong changeValue = vinTotal - sendTotal;
            SmartContractTransaction newContractScTransaction = new SmartContractTransaction
            {
                To = this.scTransaction.To,
                OpCodeType = OpcodeType.OP_CALLCONTRACT,
                MethodName = ""
            };
            Script contractScript = new Script(newContractScTransaction.ToBytes());
            tx.AddOutput(new TxOut(new Money(changeValue), contractScript));

            StoredVin newContractVin = new StoredVin
            {
                Hash = tx.GetHash(),
                Nvout = Convert.ToUInt32(tx.Outputs.Count - 1),
                Value = changeValue
            };

            this.state.SetUnspent(this.scTransaction.To, newContractVin);

            return tx;
        }

        public void SetupBalances()
        {
            foreach(var transfer in this.transfers)
            {
                // This can only come from the contract.
                if (this.txBalances.ContainsKey(transfer.To))
                    this.txBalances[transfer.To] += transfer.Value;
                else
                    this.txBalances[transfer.To] = transfer.Value;
            }
        }

    }
}
