using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core
{
    public class SmartContractTransactionContext : ISmartContractTransactionContext
    {
        private readonly ulong blockHeight;

        private readonly uint160 coinbaseAddress;

        private readonly Transaction transaction;

        private readonly TxOut contractTxOut;

        private readonly uint160 sender;

        private readonly Money mempoolFee;

        /// <inheritdoc />
        public bool IsCreate
        {
            get { return this.contractTxOut.ScriptPubKey.IsSmartContractCreate(); }
        }

        /// <inheritdoc />
        public bool IsCall
        {
            get { return this.contractTxOut.ScriptPubKey.IsSmartContractCall(); }
        }

        /// <inheritdoc />
        public uint256 TransactionHash
        {
            get { return this.transaction.GetHash(); }
        }

        /// <inheritdoc />
        public uint160 Sender
        {
            get { return this.sender; }
        }

        /// <inheritdoc />
        public ulong TxOutValue
        {
            get { return this.contractTxOut.Value; }
        }

        /// <inheritdoc />
        public uint Nvout
        {
            get { return (uint) this.transaction.Outputs.IndexOf(this.contractTxOut); }
        }

        /// <inheritdoc />
        public IEnumerable<byte> ContractData
        {
            get { return this.contractTxOut.ScriptPubKey.ToBytes().Skip(1); }
        }

        public Script ScriptPubKey => this.contractTxOut.ScriptPubKey;

        /// <inheritdoc />
        public Money MempoolFee
        {
            get { return this.mempoolFee; }
        }

        /// <inheritdoc />
        public uint160 CoinbaseAddress
        {
            get { return this.coinbaseAddress; }
        }

        /// <inheritdoc />
        public ulong BlockHeight
        {
            get { return this.blockHeight; }
        }

        public SmartContractTransactionContext(
            ulong blockHeight,
            uint160 coinbaseAddress,
            Money mempoolFee,
            uint160 sender,
            Transaction transaction)
        {
            this.blockHeight = blockHeight;
            this.coinbaseAddress = coinbaseAddress;
            this.transaction = transaction;
            this.contractTxOut = transaction.Outputs.FirstOrDefault(x => x.ScriptPubKey.IsSmartContractExec());
            Guard.NotNull(this.contractTxOut, nameof(this.contractTxOut));

            this.sender = sender;
            this.mempoolFee = mempoolFee;
        }
    }
}
