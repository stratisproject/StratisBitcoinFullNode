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

            Transaction tx = new Transaction();

            // create inputs from stored vins - Possibly 1 from previous vin and possibly 1 if this transaction has value
            ulong vinTotal = 0;
            foreach(StoredVin vin in this.unspents)
            {
                OutPoint outpoint = new OutPoint(vin.Hash, vin.Nvout);
                tx.AddInput(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
                vinTotal += vin.Value;
            }

            foreach(TxOut txOut in GetOutputs())
            {
                tx.Outputs.Add(txOut);
            }

            // create 'change' txOut for contract
            ulong changeValue = vinTotal - tx.TotalOut;
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

            //Update db to reflect new unspent for contract.
            this.state.SetUnspent(this.scTransaction.To, newContractVin);

            return tx;
        }

        private IList<TxOut> GetOutputs()
        {
            List<TxOut> txOuts = new List<TxOut>();

            foreach (KeyValuePair<uint160, ulong> b in this.txBalances)
            {
                Script script = GetTxOutScriptForAddress(b.Key);
                txOuts.Add(new TxOut(new Money(b.Value), script));
            }
            return txOuts;
        }

        /// <summary>
        /// Gets the script used to 'send' an address funds, depending on whether it's a contract or non-contract.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private Script GetTxOutScriptForAddress(uint160 address)
        {
            AccountState a = this.state.GetAccountState(address);
            if (a != null)
            {
                // This is meant to be a 'callcontract' with 0 for all parameters - and it should never be executed itself. It exists inside the execution of another contract.
                SmartContractTransaction newScTransaction = new SmartContractTransaction
                {
                    To = address,
                    OpCodeType = OpcodeType.OP_CALLCONTRACT,
                    MethodName = ""
                };
                return new Script(newScTransaction.ToBytes());
            }

            return new Script(
                        OpcodeType.OP_DUP,
                        OpcodeType.OP_HASH160,
                        Op.GetPushOp(address.ToBytes()),
                        OpcodeType.OP_EQUALVERIFY,
                        OpcodeType.OP_CHECKSIG
                    ); 
        }


        /// <summary>
        /// Note: As of right now, transfers are only coming from the contract. Haven't yet started sending funds all over. 
        /// Sets up balances to be sent via the outputs of the tx.
        /// </summary>
        private void SetupBalances()
        {
            foreach(TransferInfo transfer in this.transfers)
            {
                if (this.txBalances.ContainsKey(transfer.To))
                    this.txBalances[transfer.To] += transfer.Value;
                else
                    this.txBalances[transfer.To] = transfer.Value;
            }
        }

    }
}
