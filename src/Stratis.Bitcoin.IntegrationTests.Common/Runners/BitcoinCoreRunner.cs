using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class BitcoinCoreRunner : NodeRunner
    {
        private readonly string bitcoinDPath;
        private Process process;
        private int processID;

        public BitcoinCoreRunner(string dataDir, string bitcoinDPath)
            : base(dataDir)
        {
            this.bitcoinDPath = bitcoinDPath;
            this.Network = KnownNetworks.RegTest;
        }

        public new bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public new void Kill()
        {
            TimeSpan duration = TimeSpan.FromSeconds(30);
            TestHelper.WaitLoop(() =>
            {
                try
                {
                    if (this.IsDisposed) return true;
                    this.process.Kill();
                    this.process.WaitForExit();
                    return true;
                }
                catch (Exception e)
                {
                    return false;
                }
            }, cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason: $"Failed to kill {this.GetType()} process number:{this.processID} within {duration} seconds");
        }

        public override void OnStart()
        {
            string logMode = Debugger.IsAttached ? "-debug=net" : string.Empty;
            this.process = Process.Start(new FileInfo(this.bitcoinDPath).FullName, 
                $"-conf=bitcoin.conf -datadir={this.DataFolder} {logMode}");
            this.processID = this.process?.Id ?? 0;
        }

        public override void BuildNode()
        {
        }
    }
}