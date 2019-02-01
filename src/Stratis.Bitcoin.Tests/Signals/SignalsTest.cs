﻿using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalsTest
    {
        private readonly Bitcoin.Signals.Signals signals;

        public SignalsTest()
        {
            this.signals = new Bitcoin.Signals.Signals();
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);

            bool signaled = false;
            this.signals.OnBlockConnected += delegate (ChainedHeaderBlock headerBlock) { signaled = true; };

            this.signals.TriggerBlockConnected(chainedHeaderBlock);

            Assert.True(signaled);
        }

        [Fact]
        public void SignalBlockDisconnectedToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);

            bool signaled = false;
            this.signals.OnBlockDisconnected += delegate(ChainedHeaderBlock headerBlock) { signaled = true; };

            this.signals.TriggerBlockDisconnected(chainedHeaderBlock);

            Assert.True(signaled);
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();

            bool signaled = false;
            this.signals.OnTransactionAvailable += delegate(Transaction transaction1) { signaled = true; };

            this.signals.TriggerTransactionAvailable(transaction);

            Assert.True(signaled);
        }
    }
}