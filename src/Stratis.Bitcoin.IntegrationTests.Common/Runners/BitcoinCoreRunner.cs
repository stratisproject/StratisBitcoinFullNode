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

        public BitcoinCoreRunner(string dataDir, string bitcoinDPath)
            : base(dataDir)
        {
            this.bitcoinDPath = bitcoinDPath;
            this.Network = KnownNetworks.RegTest;
        }

        public new bool IsDisposed
        {
            get
            {
                return this.process == null || (this.process?.HasExited == true);
            }
        }

        public override void Kill()
        {
            TimeSpan duration = TimeSpan.FromSeconds(30);
            TestHelper.WaitLoop(() =>
            {
                try
                {
                    if (this.IsDisposed) return true;

                    this.process.Kill();
                    this.process.WaitForExit(15000);

                    return false;
                }
                catch (Exception e)
                {
                    return false;
                }
            }, cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason: $"Failed to kill {this.GetType()} process number:{this.process.Id} within {duration} seconds");
        }

        public override void OnStart()
        {
            string logMode = Debugger.IsAttached ? "-debug=net" : string.Empty;
            TimeSpan duration = TimeSpan.FromSeconds(15);

            TestHelper.WaitLoop(() =>
            {
                try
                {
                    this.process = Process.Start(new FileInfo(this.bitcoinDPath).FullName,
                        $"-conf=bitcoin.conf -datadir={this.DataFolder} {logMode}");
                    return true;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason:$"Failed to start BitcoinD within {duration} seconds");
        }

        public override void BuildNode()
        {
        }
    }
}