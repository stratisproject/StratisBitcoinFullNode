using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Stratis.Bitcoin.Controllers.Converters;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// Lists the possible states in which a cross chain transfer can be found.
    /// </summary>
    public class TransferStatus
    {
        protected TransferStatus() { }

        /// <summary>First status, when we only know about an existing deposit on the source chain.</summary>
        public static TransferStatus DepositFound = new TransferStatus();

        /// <summary>The partial transaction for a deposit found on source chain is being prepared.</summary>
        public static TransferStatus PartialTransactionPreparing = new TransferStatus();

        /// <summary>The preparation of the partial transaction has stalled due to a lack of matured UTXOs.</summary>
        public static TransferStatus PartialTransactionPendingUtxo = new TransferStatus();
        
        /// <summary>The partial transaction is pending enough signatures to reach quorum.</summary>
        public static TransferStatus PartialTransactionPendingSignatures = new TransferStatus();

        /// <summary>The partial transaction is pending enough signatures to reach quorum.</summary>
        public static TransferStatus PartialTransactionSigned = new TransferStatus();

        /// <summary>For a transaction that cannot be tied back to an existing deposit on the source chain.</summary>
        public static TransferStatus PartialTransactionRejected = new TransferStatus();

        /// <summary>That status might only apply to the leader: the signed transaction has reached quorum and been broadcast.</summary>
        public static TransferStatus TransactionBroadcast = new TransferStatus();

        /// <summary>The transaction has has been seen on the mempool.</summary>
        public static TransferStatus TransactionInMempool = new TransferStatus();

        /// <summary>The withdrawal of funds from multisig has has been matched on chain.</summary>
        public static TransferStatus WithdrawalOnChain = new TransferStatus();

        /// <summary>The withdrawal of funds from multisig has has been matched on chain and passed max reorg of the given chain.</summary>
        public static TransferStatus WithdrawalMaturedOnChain = new TransferStatus();

        public static IReadOnlyList<TransferStatus> GetAll()
        {
            return typeof(TransferStatus)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(TransferStatus))
                .Select(f => (TransferStatus)f.GetValue(null))
                .ToList().AsReadOnly();
        }

        public static IReadOnlyList<string> GetAllAsString()
        {
            return typeof(TransferStatus)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(TransferStatus))
                .Select(f => f.Name)
                .ToList().AsReadOnly();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return typeof(TransferStatus)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(TransferStatus) )
                .Select(f => ((TransferStatus)f.GetValue(null), f.Name))
                .Single(s => s.Item1 == this).Item2;
        }

        /// <inheritdoc />
        public string Name => this.ToString();
    }

}
