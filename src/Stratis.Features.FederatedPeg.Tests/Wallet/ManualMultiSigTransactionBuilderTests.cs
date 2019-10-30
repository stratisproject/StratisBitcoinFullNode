using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Policy;
using NSubstitute;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Features.FederatedPeg.Interfaces;
using Xunit;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;

namespace Stratis.Features.FederatedPeg.Tests.Wallet
{
    public class ManualMultiSigTransactionBuilderTests
    {
        private readonly Network network;
        private readonly IFederatedPegSettings federatedPegSettings;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly IMultisigCoinSelector multisigCoinSelector;

        public ManualMultiSigTransactionBuilderTests()
        {
            this.network = new StratisTest();
            this.federatedPegSettings = Substitute.For<IFederatedPegSettings>();
            this.walletFeePolicy = Substitute.For<IWalletFeePolicy>();
            this.multisigCoinSelector = Substitute.For<IMultisigCoinSelector>();
        }

        [Fact]
        public void Multisig_Withdrawal_Transaction_Builds_Correctly()
        {
            var keys = new Key[]
            {
                new Key(),
                new Key(),
                new Key()
            };

            Script redeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, keys.Select(k => k.PubKey).ToArray());

            BitcoinAddress multiSigAddress = redeemScript.Hash.GetAddress(this.network);

            this.federatedPegSettings.MultiSigAddress.Returns(multiSigAddress);

            this.walletFeePolicy.GetFeeRate(Arg.Any<int>()).Returns(new FeeRate(10000));

            var coins = new List<Coin>
            {
                new ScriptCoin(new OutPoint(new uint256(), 0), new TxOut(new Money(80, MoneyUnit.BTC), redeemScript.Hash.ScriptPubKey), redeemScript),
                new ScriptCoin(new OutPoint(new uint256(1), 0), new TxOut(new Money(20, MoneyUnit.BTC), redeemScript.Hash.ScriptPubKey), redeemScript)
            };

            this.multisigCoinSelector.SelectCoins(Arg.Any<List<Recipient>>()).Returns((coins, null));

            var builder = new FedMultiSigManualWithdrawalTransactionBuilder(this.network, this.federatedPegSettings, this.walletFeePolicy, this.multisigCoinSelector);

            var recipients = new List<Recipient>
            {
                new Recipient
                {
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey),
                    Amount = new Money(40, MoneyUnit.BTC)
                },
                new Recipient
                {
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey),
                    Amount = new Money(45, MoneyUnit.BTC)
                }
            };

            Transaction transaction = builder.BuildTransaction(recipients, keys);

            // Check the amount & recipients are correct
            Assert.Equal(recipients[0].Amount, (Money) transaction.Outputs.Where(r => r.ScriptPubKey == recipients[0].ScriptPubKey).Sum(o => o.Value));
            Assert.Equal(recipients[1].Amount, (Money) transaction.Outputs.Where(r => r.ScriptPubKey == recipients[1].ScriptPubKey).Sum(o => o.Value));

            // Check the fee is correct
            Assert.Equal(transaction.GetFee(coins.ToArray()), coins.Sum(c => c.Amount) - transaction.TotalOut);

            // Check the change address is correct
            Assert.Equal(multiSigAddress.ScriptPubKey, transaction.Outputs.First(o => !recipients.Select(recipient => recipient.ScriptPubKey).Contains(o.ScriptPubKey)).ScriptPubKey);
        }

        [Fact]
        public void Build_Withdrawal_Transaction_Throws_Exception_If_Not_Enough_Fees()
        {
            var keys = new Key[]
            {
                new Key(),
                new Key(),
                new Key()
            };

            Script redeemScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(2, keys.Select(k => k.PubKey).ToArray());

            BitcoinAddress multiSigAddress = redeemScript.Hash.GetAddress(network);
            
            this.federatedPegSettings.MultiSigAddress.Returns(multiSigAddress);

            this.walletFeePolicy.GetFeeRate(Arg.Any<int>()).Returns(new FeeRate(10000));

            var coins = new List<Coin>
            {
                new ScriptCoin(new OutPoint(new uint256(), 0), new TxOut(new Money(80, MoneyUnit.BTC), redeemScript.Hash.ScriptPubKey), redeemScript),
                new ScriptCoin(new OutPoint(new uint256(1), 0), new TxOut(new Money(20, MoneyUnit.BTC), redeemScript.Hash.ScriptPubKey), redeemScript)
            };

            this.multisigCoinSelector.SelectCoins(Arg.Any<List<Recipient>>()).Returns((coins, null));

            var builder = new FedMultiSigManualWithdrawalTransactionBuilder(this.network, this.federatedPegSettings, this.walletFeePolicy, this.multisigCoinSelector);

            var recipients = new List<Recipient>
            {
                new Recipient
                {
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey),
                    Amount = new Money(100, MoneyUnit.BTC)
                }
            };

            Assert.Throws<WalletException>(() => builder.BuildTransaction(recipients, keys));
        }
    }
}