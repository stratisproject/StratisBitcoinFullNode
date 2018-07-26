﻿using System.Diagnostics;
using System.IO;
using NBitcoin;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class BitcoinCoreRunner : NodeRunner
    {
        private readonly string bitcoinDPath;
        private Process process;

        public BitcoinCoreRunner(string dataDir, string bitcoinDPath)
            : base(dataDir)
        {
            this.bitcoinDPath = bitcoinDPath;
            this.Network = Networks.RegTest;
        }

        public new bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public new void Kill()
        {
            if (!this.IsDisposed)
            {
                this.process.Kill();
                this.process.WaitForExit();
            }
        }

        public override void OnStart()
        {
            this.process = Process.Start(new FileInfo(this.bitcoinDPath).FullName, $"-conf=bitcoin.conf -datadir={this.DataFolder} -debug=net");
        }

        public override void BuildNode()
        {
        }
    }
}