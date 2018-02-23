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
        /// The index of each address's new balance output.
        /// </summary>
        private readonly Dictionary<uint160, uint> nVouts;

        /// <summary>
        /// The smart contract transaction that initiated this whole execution.
        /// </summary>
        private readonly SmartContractCarrier smartContractCarrier;

        /// <summary>
        /// Reference to the current smart contract state.
        /// </summary>
        private readonly IContractStateRepository stateRepository;

        /// <summary>
        /// All of the transfers that happened internally inside of the contract execution.
        /// </summary>
        private readonly IList<TransferInfo> transfers;

        /// <summary>
        /// New balances for each address involved through all transfers made.
        /// </summary>
        private readonly Dictionary<uint160, ulong> txBalances;

        /// <summary>
        /// The current unspents for each contract. Only ever one per contract.
        /// </summary>
        private IList<ContractUnspentOutput> unspents;

        public CondensingTx(SmartContractCarrier smartContractCarrier)
        {
            this.smartContractCarrier = smartContractCarrier;

            this.nVouts = new Dictionary<uint160, uint>();
            this.txBalances = new Dictionary<uint160, ulong>();
            this.unspents = new List<ContractUnspentOutput>();
        }

        public CondensingTx(SmartContractCarrier smartContractCarrier, IList<TransferInfo> transfers, IContractStateRepository stateRepository)
            : this(smartContractCarrier)
        {
            this.stateRepository = stateRepository;
            this.transfers = transfers;
        }

        /// <summary>
        /// Should contract execution fail, we need to send the money, that was
        /// sent to contract, back to the contract's sender.
        /// </summary>
        public Transaction CreateRefundTransaction()
        {
            var tx = new Transaction();

            //Spend the input on the contract----------------------------------
            var outpoint = new OutPoint(this.smartContractCarrier.TransactionHash, this.smartContractCarrier.Nvout);
            tx.AddInput(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
            //-----------------------------------------------------------------

            //Create refund unspent TxOut--------------------------------------
            Script script = CreateScript(this.smartContractCarrier.Sender);
            var txOut = new TxOut(new Money(this.smartContractCarrier.TxOutValue), script);
            tx.Outputs.Add(txOut);
            //-----------------------------------------------------------------

            return tx;
        }

        /// <summary>
        /// Builds the transaction that updates everyone's balances, which is to be appended to the block.
        /// </summary>
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
        private Transaction BuildTransaction()
        {
            var tx = new Transaction();

            foreach (ContractUnspentOutput vin in this.unspents)
            {
                var outpoint = new OutPoint(vin.Hash, vin.Nvout);
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
                if (this.stateRepository.GetAccountState(kvp.Key) != null)
                {
                    var newContractVin = new ContractUnspentOutput
                    {
                        Hash = tx.GetHash(),
                        Nvout = this.nVouts[kvp.Key],
                        Value = kvp.Value
                    };

                    this.stateRepository.SetUnspent(kvp.Key, newContractVin);
                }
            }
        }

        /// <summary>
        /// Get the outputs for the condensing transaction
        /// </summary>
        private IList<TxOut> GetOutputs()
        {
            var txOuts = new List<TxOut>();

            // Order by descending for now. Easier to test. TODO: Worth changing in long run?
            foreach (KeyValuePair<uint160, ulong> b in this.txBalances.OrderByDescending(x => x.Value).Where(x => x.Value > 0))
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
        /// <param name="address">The address of the receiver.</param>
        private Script GetTxOutScriptForAddress(uint160 address)
        {
            AccountState accountState = this.stateRepository.GetAccountState(address);
            if (accountState != null)
            {
                // This is meant to be a 'callcontract' with 0 for all parameters - and it should never be executed itself. It exists inside the execution of another contract.
                var newSmartContractCarrier = SmartContractCarrier.CallContract(1, address, string.Empty, 0, Gas.None);
                return new Script(newSmartContractCarrier.Serialize());
            }

            return CreateScript(address);
        }

        /// <summary>
        /// Creates a script to send funds to a given address.
        /// </summary>
        /// <param name="address">The address of the receiver.</param>
        private Script CreateScript(uint160 address)
        {
            var script = new Script(
                OpcodeType.OP_DUP,
                OpcodeType.OP_HASH160,
                Op.GetPushOp(address.ToBytes()),
                OpcodeType.OP_EQUALVERIFY,
                OpcodeType.OP_CHECKSIG
            );
            return script;
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
            var uniqueAddresses = new HashSet<uint160>
            {
                this.smartContractCarrier.To
            };

            foreach (TransferInfo transferInfo in this.transfers)
            {
                uniqueAddresses.Add(transferInfo.To);
                uniqueAddresses.Add(transferInfo.From);
            }

            foreach (uint160 unique in uniqueAddresses)
            {
                ContractUnspentOutput unspent = this.stateRepository.GetUnspent(unique);
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