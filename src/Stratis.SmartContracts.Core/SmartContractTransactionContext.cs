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

        public bool IsCreate
        {
            get { return this.contractTxOut.ScriptPubKey.IsSmartContractCreate; }
        }

        public bool IsCall
        {
            get { return this.contractTxOut.ScriptPubKey.IsSmartContractCall; }
        }

        public uint256 TransactionHash
        {
            get { return this.transaction.GetHash(); }
        }

        public uint160 Sender
        {
            get { return this.sender; }
        }

        public ulong TxOutValue
        {
            get { return this.contractTxOut.Value; }
        }

        public uint Nvout
        {
            get { return (uint) this.transaction.Outputs.IndexOf(this.contractTxOut); }
        }

        public IEnumerable<byte> ContractData
        {
            get { return this.contractTxOut.ScriptPubKey.ToBytes().Skip(1); }
        }

        public Money MempoolFee
        {
            get { return this.mempoolFee; }
        }

        public uint160 CoinbaseAddress
        {
            get { return this.coinbaseAddress; }
        }

        public ulong BlockHeight
        {
            get { return this.blockHeight; }
        }

        public Transaction Transaction
        {
            get { return this.transaction; }
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
            this.contractTxOut = transaction.Outputs.FirstOrDefault(x => x.ScriptPubKey.IsSmartContractExec);
            Guard.NotNull(this.contractTxOut, nameof(this.contractTxOut));

            this.sender = sender;
            this.mempoolFee = mempoolFee;
        }
    }
}
