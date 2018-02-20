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
        /// <summary>
        /// The smart contract transaction that initiated this whole execution.
        /// </summary>
        private SmartContractCarrier smartContractCarrier;

        /// <summary>
        /// All of the transfers that happened internally inside of the contract execution.
        /// </summary>
        private IList<TransferInfo> transfers;

        /// <summary>
        /// The current unspents for each contract. Only ever one per contract.
        /// </summary>
        private IList<ContractUnspentOutput> unspents;

        /// <summary>
        /// Reference to the current smart contract state.
        /// </summary>
        private IContractStateRepository state;

        /// <summary>
        /// New balances for each address involved through all transfers made.
        /// </summary>
        private Dictionary<uint160, ulong> txBalances;

        /// <summary>
        /// The index of each address's new balance output.
        /// </summary>
        private Dictionary<uint160, uint> nVouts;

        public CondensingTx(SmartContractCarrier smartContractCarrier, IList<TransferInfo> transfers, IContractStateRepository state)
        {
            this.smartContractCarrier = smartContractCarrier;
            this.transfers = transfers;
            this.state = state;
            this.unspents = new List<ContractUnspentOutput>();
            this.txBalances = new Dictionary<uint160, ulong>();
            this.nVouts = new Dictionary<uint160, uint>();
        }

        /// <summary>
        /// Builds the transaction that updates everyone's balances, which is to be appended to the block.
        /// </summary>
        /// <returns></returns>
        public Transaction CreateCondensingTransaction()
        {
            SetupBalances();
            Transaction tx = BuildTransaction();
            UpdateStateUnspents(tx);
            return tx;
        }

        /// <summary>
        /// Builds the transaction to be appended to the block.
        /// </summary>
        /// <returns></returns>
        private Transaction BuildTransaction()
        {
            Transaction tx = new Transaction();

            foreach (ContractUnspentOutput vin in this.unspents)
            {
                OutPoint outpoint = new OutPoint(vin.Hash, vin.Nvout);
                tx.AddInput(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
            }

            foreach (TxOut txOut in GetOutputs())
            {
                tx.Outputs.Add(txOut);
            }

            return tx;
        }


        /// <summary>
        /// Update the database to reflect the new UTXOs assigned to each contract.
        /// </summary>
        private void UpdateStateUnspents(Transaction tx)
        {
            foreach (KeyValuePair<uint160, ulong> kvp in this.txBalances)
            {
                if (this.state.GetAccountState(kvp.Key) != null)
                {
                    ContractUnspentOutput newContractVin = new ContractUnspentOutput
                    {
                        Hash = tx.GetHash(),
                        Nvout = this.nVouts[kvp.Key],
                        Value = kvp.Value
                    };
                    this.state.SetUnspent(kvp.Key, newContractVin);
                }
            }
        }

        /// <summary>
        /// Get the outputs for the condensing transaction
        /// </summary>
        /// <returns></returns>
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
                this.unspents.Add(new ContractUnspentOutput
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
                ContractUnspentOutput unspent = this.state.GetUnspent(unique);
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