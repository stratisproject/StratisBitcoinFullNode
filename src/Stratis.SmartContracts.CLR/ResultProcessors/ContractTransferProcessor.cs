using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR.ResultProcessors
{
    public class ContractTransferProcessor : IContractTransferProcessor
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public ContractTransferProcessor(ILoggerFactory loggerFactory, Network network)
        {
            this.loggerFactory = loggerFactory;
            this.network = network;
        }

        /// <inheritdoc />
        public Transaction Process(IStateRepository stateSnapshot,
            uint160 contractAddress,
            IContractTransactionContext transactionContext,
            IReadOnlyList<TransferInfo> internalTransfers,
            bool reversionRequired)
        {
            if (reversionRequired)
            {
                // Send back funds
                if (transactionContext.TxOutValue > 0)
                {
                    return this.CreateRefundTransaction(transactionContext);
                }

                return null;
            }

            // If contract received no funds and made no transfers, do nothing.
            if (transactionContext.TxOutValue == 0 && !internalTransfers.Any( x=> x.Value > 0))
            {
                return null;
            }

            // If contract had no balance, received funds, but made no transfers, assign the current UTXO.
            if (stateSnapshot.GetUnspent(contractAddress) == null && transactionContext.TxOutValue > 0 && !internalTransfers.Any())
            {
                stateSnapshot.SetUnspent(contractAddress, new ContractUnspentOutput
                {
                    Value = transactionContext.TxOutValue,
                    Hash = transactionContext.TransactionHash,
                    Nvout = transactionContext.Nvout
                });

                return null;
            }

            // All other cases we need a condensing transaction
            var transactionCondenser = new TransactionCondenser(contractAddress, this.loggerFactory, internalTransfers, stateSnapshot, this.network, transactionContext);
            return transactionCondenser.CreateCondensingTransaction();
        }

        /// <summary>
        /// Should contract execution fail, we need to send the money, that was
        /// sent to contract, back to the contract's sender.
        /// </summary>
        private Transaction CreateRefundTransaction(IContractTransactionContext transactionContext)
        {
            Transaction tx = this.network.CreateTransaction();
            tx.Time = transactionContext.Time;

            // Input from contract call
            var outpoint = new OutPoint(transactionContext.TransactionHash, transactionContext.Nvout);
            tx.AddInput(new TxIn(outpoint, new Script(new[] { (byte)ScOpcodeType.OP_SPEND })));

            // Refund output
            Script script = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(transactionContext.Sender));
            var txOut = new TxOut(new Money(transactionContext.TxOutValue), script);
            tx.Outputs.Add(txOut);
            return tx;
        }
    }
}