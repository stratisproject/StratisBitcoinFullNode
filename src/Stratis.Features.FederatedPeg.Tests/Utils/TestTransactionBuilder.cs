using NBitcoin;

namespace Stratis.Features.FederatedPeg.Tests.Utils
{
    public class TestTransactionBuilder
    {
        protected Coin RandomCoin(Money amount, Script scriptPubKey, bool p2sh)
        {
            OutPoint outpoint = RandomOutpoint();
            return p2sh
                       ? new ScriptCoin(outpoint, new TxOut(amount, scriptPubKey.Hash), scriptPubKey)
                       : new Coin(outpoint, new TxOut(amount, scriptPubKey));
        }

        protected Coin RandomCoin(Money amount, Key receiver)
        {
            OutPoint outpoint = RandomOutpoint();
            return new Coin(outpoint, new TxOut(amount, receiver));
        }

        private OutPoint RandomOutpoint()
        {
            return new OutPoint(TestingValues.GetUint256(), 0);
        }

        public Transaction BuildTransaction(IDestination receiverAddress, Money amount = null)
        {
            var transaction = new Transaction();
            transaction.AddOutput(new TxOut(amount ?? TestingValues.GetMoney(), receiverAddress));
            return transaction;
        }

        public Transaction BuildOpReturnTransaction(IDestination receiverAddress, byte[] opReturnBytes, Money amount = null)
        {
            Transaction transaction = this.BuildTransaction(receiverAddress, amount)
                .AddOpReturn(opReturnBytes);
            return transaction;
        }

        protected Transaction GetTransactionWithInputs(
            Network network,
            Key[] senderSecrets,
            Script senderScript,
            Script receiverScript,
            byte[] opReturnBytes = null,
            Money amount = null,
            bool withChange = true)
        {
            var txBuilder = new TransactionBuilder(network);
            amount = amount ?? TestingValues.GetMoney();
            Money change = withChange ? TestingValues.GetMoney() : Money.Zero;
            var multisigCoins = new ICoin[] { RandomCoin(amount + change, senderScript, true) };

            txBuilder
                .AddCoins(multisigCoins)
                .Send(receiverScript, amount)
                .SetChange(senderScript)
                .BuildTransaction(false);

            txBuilder.AddKeys(senderSecrets);

            Transaction signed = txBuilder.BuildTransaction(true);

            if (opReturnBytes != null) signed.AddOpReturn(opReturnBytes);

            return signed;
        }

        public Transaction GetTransactionWithInputs(
            Network network,
            Key senderSecret,
            Script receiverScript,
            byte[] opReturnBytes = null,
            Money amount = null,
            bool withChange = true)
        {
            Key[] senderSecrets = new[] { senderSecret };
            Script senderScript = senderSecret.ScriptPubKey;
            return GetTransactionWithInputs(
                network,
                senderSecrets,
                senderScript,
                receiverScript,
                opReturnBytes,
                amount,
                withChange);
        }
    }

    public class TestMultisigTransactionBuilder : TestTransactionBuilder
    {
        private readonly MultisigAddressHelper multisigAddressHelper;

        public Network Network { get; }

        public TestMultisigTransactionBuilder(MultisigAddressHelper multisigAddressHelper)
        {
            this.multisigAddressHelper = multisigAddressHelper;
            this.Network = multisigAddressHelper.TargetChainNetwork;
        }

        public Transaction GetWithdrawalOutOfMultisigTo(Script receiverScript, byte[] opReturnBytes, Money amount = null, bool withChange = true)
        {
            return this.GetTransactionWithInputs(
                this.Network,
                this.multisigAddressHelper.MultisigPrivateKeys,
                this.multisigAddressHelper.PayToMultiSig,
                receiverScript,
                opReturnBytes,
                amount,
                withChange);
        }
    }

    public static class TransactionExtensions
    {
        public static Transaction AddOpReturn(this Transaction transaction, byte[] opReturnContent)
        {
            transaction.AddOutput(new TxOut(Money.Zero, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(opReturnContent))));
            return transaction;
        }
    }
}