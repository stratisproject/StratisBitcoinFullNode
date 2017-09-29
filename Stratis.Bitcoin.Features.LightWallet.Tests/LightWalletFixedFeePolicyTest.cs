using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    /// <summary>
    /// Unit tests for light wallet's fixed fee policy.
    /// </summary>
    public class LightWalletFixedFeePolicyTest
    {
        /// <summary>Constructs a fixed fee policy using default node settings.</summary>
        private LightWalletFixedFeePolicy PolicyForDefaultSettings
        {  get
            {
                ILoggerFactory loggerFactory = new LoggerFactory();
                NodeSettings settings = NodeSettings.Default();
                LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, settings);
                return policy;
            }
        }

        [Fact]
        public void MinTxFee_FromDefaultSettings_IsDefaultNodeMinTxFee()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            NodeSettings settings = NodeSettings.Default();
            LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, settings);
            Assert.Equal(settings.MinTxFee, policy.MinTxFee);
        }

        [Fact]
        public void TxFeeRate_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            Assert.Equal(policy.MinTxFee, policy.TxFeeRate);
        }

        [Fact]
        public void GetFeeRate_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            Assert.Equal(policy.MinTxFee, policy.GetFeeRate(6));
        }

        [Fact]
        public void GetMinimumFee_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            const int txSizeBytes = 5000;
            Assert.Equal(policy.MinTxFee.GetFee(txSizeBytes), policy.GetMinimumFee(txSizeBytes,6));
            Assert.Equal(policy.MinTxFee.GetFee(txSizeBytes), policy.GetMinimumFee(txSizeBytes, 6, new Money(1)));
        }

        [Fact]
        public void GetRequiredFee_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            const int txSizeBytes = 5000;
            Assert.Equal(policy.MinTxFee.GetFee(txSizeBytes), policy.GetRequiredFee(txSizeBytes));
        }

        [Fact]
        public void MinTxFee_OnStratisNetwork_IsStratisDefault()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            NodeSettings settings = NodeSettings.FromArguments(new string[0], innerNetwork: Network.StratisTest);
            LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, settings);
            Assert.Equal(new FeeRate(Network.StratisTest.DefaultMinTxFee()), policy.MinTxFee);
        }

        [Fact]
        public void MinTxFee_OnBitcoinNetwork_IsBitcoinDefault()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            NodeSettings settings = NodeSettings.FromArguments(new string[0], innerNetwork: Network.TestNet);
            LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, settings);
            Assert.Equal(new FeeRate(Network.TestNet.DefaultMinTxFee()), policy.MinTxFee);
        }
    }
}
