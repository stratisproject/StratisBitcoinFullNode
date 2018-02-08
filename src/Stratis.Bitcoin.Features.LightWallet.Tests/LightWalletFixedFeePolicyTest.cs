﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Features.LightWallet.Tests
{
    /// <summary>
    /// Unit tests for light wallet's fixed fee policy.
    /// </summary>
    public class LightWalletFixedFeePolicyTest
    {
        /// <summary>Constructs a fixed fee policy using default node settings.</summary>
        private LightWalletFixedFeePolicy PolicyForDefaultSettings
        {
            get
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
            Assert.Equal(settings.FallbackTxFeeRate, policy.FallbackTxFeeRate);
        }

        [Fact]
        public void TxFeeRate_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            Assert.Equal(policy.FallbackTxFeeRate, policy.TxFeeRate);
        }

        [Fact]
        public void GetFeeRate_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            Assert.Equal(policy.FallbackTxFeeRate, policy.GetFeeRate(6));
        }

        [Fact]
        public void GetMinimumFee_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            const int TxSizeBytes = 5000;
            Assert.Equal(policy.FallbackTxFeeRate.GetFee(TxSizeBytes), policy.GetMinimumFee(TxSizeBytes, 6));
            Assert.Equal(policy.FallbackTxFeeRate.GetFee(TxSizeBytes), policy.GetMinimumFee(TxSizeBytes, 6, new Money(1)));
        }

        [Fact]
        public void GetRequiredFee_Result_IsMinTxFee()
        {
            LightWalletFixedFeePolicy policy = this.PolicyForDefaultSettings;
            const int TxSizeBytes = 5000;
            Assert.Equal(policy.FallbackTxFeeRate.GetFee(TxSizeBytes), policy.GetRequiredFee(TxSizeBytes));
        }

        [Fact]
        public void MinTxFee_OnStratisNetwork_IsStratisDefault()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            NodeSettings nodeSettings = new NodeSettings(Network.StratisTest);
            LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, nodeSettings);
            Assert.Equal(new FeeRate(Network.StratisTest.FallbackFee), policy.FallbackTxFeeRate);
        }

        [Fact]
        public void MinTxFee_OnBitcoinNetwork_IsBitcoinDefault()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            NodeSettings nodeSettings = new NodeSettings(Network.TestNet);
            LightWalletFixedFeePolicy policy = new LightWalletFixedFeePolicy(loggerFactory, nodeSettings);
            Assert.Equal(new FeeRate(Network.TestNet.FallbackFee), policy.FallbackTxFeeRate);
        }
    }
}
