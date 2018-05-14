using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.Core.State.AccountAbstractionLayer
{
    /// <summary>
    /// When a contract sends or receives funds, we need to rearrange the UTXOs addressed to it and to others so everyone ends up with the correct balances.
    /// The condensing transaction aids this process.
    /// </summary>
    public class CondensingTx
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

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
        private readonly IList<ContractUnspentOutput> unspents;

        private readonly Network network;

        public CondensingTx(ILoggerFactory loggerFactory, SmartContractCarrier smartContractCarrier, Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());

            this.smartContractCarrier = smartContractCarrier;

            this.nVouts = new Dictionary<uint160, uint>();
            this.txBalances = new Dictionary<uint160, ulong>();
            this.unspents = new List<ContractUnspentOutput>();
            this.network = network;
        }

        public CondensingTx(ILoggerFactory loggerFactory, SmartContractCarrier smartContractCarrier, IList<TransferInfo> transfers, IContractStateRepository stateRepository, Network network)
            : this(loggerFactory, smartContractCarrier, network)
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
            Script script = this.CreateScript(this.smartContractCarrier.Sender);
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
            this.SetupBalances();
            Transaction tx = this.BuildTransaction();
            this.UpdateStateUnspents(tx);
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

            foreach (TxOut txOut in this.GetOutputs())
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
            this.logger.LogTrace("()");

            foreach (KeyValuePair<uint160, ulong> balance in this.txBalances)
            {
                if (this.stateRepository.GetAccountState(balance.Key) != null)
                {
                    this.logger.LogTrace("{0}:{1}", nameof(balance.Key), balance.Key.ToAddress(this.network));

                    var newContractVin = new ContractUnspentOutput
                    {
                        Hash = tx.GetHash(),
                        Nvout = this.nVouts[balance.Key],
                        Value = balance.Value
                    };

                    this.stateRepository.SetUnspent(balance.Key, newContractVin);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Get the outputs for the condensing transaction
        /// </summary>
        private IList<TxOut> GetOutputs()
        {
            this.logger.LogTrace("()");

            var txOuts = new List<TxOut>();

            // Order by descending for now. Easier to test. TODO: Worth changing in long run?
            foreach (KeyValuePair<uint160, ulong> balance in this.txBalances.OrderByDescending(x => x.Value).Where(x => x.Value > 0))
            {
                Script script = this.GetTxOutScriptForAddress(balance.Key);
                txOuts.Add(new TxOut(new Money(balance.Value), script));

                this.logger.LogTrace("{0}:{1},{2}:{3}", nameof(balance.Key), balance.Key.ToAddress(this.network), nameof(txOuts.Count), txOuts.Count - 1);
                this.nVouts.Add(balance.Key, Convert.ToUInt32(txOuts.Count - 1));
            }

            this.logger.LogTrace("(-):{0}={1}", nameof(txOuts), txOuts.Count);

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
                return new Script(OpcodeType.OP_INTERNALCONTRACTTRANSFER, Op.GetPushOp(address.ToBytes()));
            }

            return this.CreateScript(address);
        }

        /// <summary>
        /// Creates a script to send funds to a given address.
        /// </summary>
        /// <param name="address">The address of the receiver.</param>
        private Script CreateScript(uint160 address)
        {
            Script script = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(address));
            return script;
        }

        private void SetupBalances()
        {
            this.logger.LogTrace("()");

            // Add the value of the initial transaction.
            if (this.smartContractCarrier.TxOutValue > 0)
            {
                this.logger.LogTrace("{0}:{1}", nameof(this.smartContractCarrier.TxOutValue), this.smartContractCarrier.TxOutValue);

                this.unspents.Add(new ContractUnspentOutput
                {
                    Hash = this.smartContractCarrier.TransactionHash,
                    Nvout = this.smartContractCarrier.Nvout,
                    Value = this.smartContractCarrier.TxOutValue
                });

                this.txBalances[this.smartContractCarrier.ContractAddress] = this.smartContractCarrier.TxOutValue;
            }

            // For each unique address, if it is a contract, get the utxo it currently holds.
            var uniqueAddresses = new HashSet<uint160>
            {
                this.smartContractCarrier.ContractAddress
            };

            foreach (TransferInfo transferInfo in this.transfers)
            {
                this.logger.LogTrace("{0}:{1},{2}:{3}", nameof(transferInfo.From), transferInfo.From.ToAddress(this.network), nameof(transferInfo.To), transferInfo.To.ToAddress(this.network));

                uniqueAddresses.Add(transferInfo.To);
                uniqueAddresses.Add(transferInfo.From);
            }

            foreach (uint160 unique in uniqueAddresses)
            {
                this.logger.LogTrace("{0}:{1}", nameof(unique), unique);

                ContractUnspentOutput unspent = this.stateRepository.GetUnspent(unique);
                if (unspent != null && unspent.Value > 0)
                {
                    this.logger.LogTrace("{0}:{1},{2}:{3}", nameof(unspent), unspent, nameof(unspent.Value), unspent.Value);

                    this.unspents.Add(unspent);

                    if (this.txBalances.ContainsKey(unique))
                    {
                        this.logger.LogTrace("[TXBALANCE_CONTAINS_KEY]");
                        this.txBalances[unique] += unspent.Value;
                    }
                    else
                    {
                        this.logger.LogTrace("[TXBALANCE_DOESNOT_CONTAIN_KEY]");
                        this.txBalances[unique] = unspent.Value;
                    }
                }
            }

            // Lastly update the funds to be distributed based on the transfers that have taken place.
            foreach (TransferInfo transfer in this.transfers.Where(x => x.Value > 0))
            {
                this.logger.LogTrace("{0}:{1},{2}:{3}", nameof(transfer), transfer.To, nameof(transfer.Value), transfer.Value);

                if (this.txBalances.ContainsKey(transfer.To))
                {
                    this.logger.LogTrace("[TXBALANCE_CONTAINS_TRANSFER_TO]");
                    this.txBalances[transfer.To] += transfer.Value;
                }
                else
                {
                    this.logger.LogTrace("[TXBALANCE_DOES_NOT_CONTAIN_TRANSFER_TO]");
                    this.txBalances[transfer.To] = transfer.Value;
                }

                this.txBalances[transfer.From] -= transfer.Value;
            }

            this.logger.LogTrace("(-)");
        }
    }
}