using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    /// <summary>
    /// When a contract sends or receives funds, we need to rearrange the UTXOs addressed to it and to others so everyone ends up with the correct balances.
    /// The condensing transaction aids this process.
    /// </summary>
    public class CondensingTx
    {
        private SmartContractCarrier smartContractCarrier;
        private IList<TransferInfo> transfers;
        private IList<StoredVin> unspents;
        private IContractStateRepository state;
        private Dictionary<uint160, ulong> txBalances;
        private Dictionary<uint160, uint> nVouts;

        public CondensingTx(SmartContractCarrier smartContractCarrier, IList<TransferInfo> transfers, IContractStateRepository state)
        {
            this.smartContractCarrier = smartContractCarrier;
            this.transfers = transfers;
            this.state = state;
            this.unspents = new List<StoredVin>();
            this.txBalances = new Dictionary<uint160, ulong>();
            this.nVouts = new Dictionary<uint160, uint>();
        }

        public Transaction CreateCondensingTransaction()
        {
            SetupBalances();




            Transaction tx = new Transaction();

            // create inputs from stored vins - Possibly 1 from previous vin and possibly 1 if this transaction has value
            ulong vinTotal = 0;
            foreach (StoredVin vin in this.unspents)
            {
                OutPoint outpoint = new OutPoint(vin.Hash, vin.Nvout);
                tx.AddInput(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
                vinTotal += vin.Value;
            }

            foreach (TxOut txOut in GetOutputs())
            {
                tx.Outputs.Add(txOut);
            }

            //Update db
            foreach(KeyValuePair<uint160, ulong> kvp in this.txBalances)
            {
                if (this.state.GetAccountState(kvp.Key) != null)
                {
                    StoredVin newContractVin = new StoredVin
                    {
                        Hash = tx.GetHash(),
                        Nvout =  this.nVouts[kvp.Key],
                        Value = kvp.Value
                    };
                    this.state.SetUnspent(kvp.Key, newContractVin);
                }
            }

            return tx;
        }

        private IList<TxOut> GetOutputs()
        {
            List<TxOut> txOuts = new List<TxOut>();
            // Order by descending for now. Easier to test. TODO: Worth changing in long run?
            foreach (KeyValuePair<uint160, ulong> b in this.txBalances.OrderByDescending(x=>x.Value).Where(x=> x.Value > 0))
            {
                Script script = GetTxOutScriptForAddress(b.Key);
                txOuts.Add(new TxOut(new Money(b.Value), script));
                this.nVouts.Add(b.Key, Convert.ToUInt32(txOuts.Count - 1));
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
                var newSmartContractCarrier = SmartContractCarrier.CallContract(1, address, string.Empty, 0, 0);
                return new Script(newSmartContractCarrier.Serialize());
            }

            return new Script(
                        OpcodeType.OP_DUP,
                        OpcodeType.OP_HASH160,
                        Op.GetPushOp(address.ToBytes()),
                        OpcodeType.OP_EQUALVERIFY,
                        OpcodeType.OP_CHECKSIG
                    );
        }

        private void SetupBalances()
        {
            // Add the value of the initial transaction.
            if (this.smartContractCarrier.TxOutValue > 0)
            {
                this.unspents.Add(new StoredVin
                {
                    Hash = this.smartContractCarrier.TransactionHash,
                    Nvout = this.smartContractCarrier.Nvout,
                    Value = this.smartContractCarrier.TxOutValue
                });
                this.txBalances[this.smartContractCarrier.To] = this.smartContractCarrier.TxOutValue;
            }

            // For each unique address, if it is a contract, get the utxo it currently holds.
            HashSet<uint160> uniqueAddresses = new HashSet<uint160>();
            uniqueAddresses.Add(this.smartContractCarrier.To);
            foreach(TransferInfo transferInfo in this.transfers)
            {
                uniqueAddresses.Add(transferInfo.To);
                uniqueAddresses.Add(transferInfo.From);
            }

            foreach (uint160 unique in uniqueAddresses)
            {
                StoredVin unspent = this.state.GetUnspent(unique);
                if (unspent != null && unspent.Value > 0)
                {
                    this.unspents.Add(unspent);
                    if (this.txBalances.ContainsKey(unique))
                        this.txBalances[unique] += unspent.Value;
                    else
                        this.txBalances[unique] = unspent.Value;
                }
            }

            // Lastly update the funds to be distributed based on the transfers that have taken place.
            foreach (TransferInfo transfer in this.transfers)
            {
                if (this.txBalances.ContainsKey(transfer.To))
                    this.txBalances[transfer.To] += transfer.Value;
                else
                    this.txBalances[transfer.To] = transfer.Value;

                this.txBalances[transfer.From] -= transfer.Value;
            }
        }
    }
}