using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class BitcoinCoreRunner : NodeRunner
    {
        private readonly string bitcoinDPath;
        private Process process;

        public BitcoinCoreRunner(string dataDir, string bitcoinDPath)
            : base(dataDir, null)
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

        public override void Stop()
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
                catch
                {
                    return false;
                }
            }, cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason: $"Failed to kill {this.GetType()} process number:{this.process.Id} within {duration} seconds");
        }

        public override void Start()
        {
            string logMode = Debugger.IsAttached ? "-debug=net" : string.Empty;
            TimeSpan duration = TimeSpan.FromSeconds(15);

            // The complete path bitcoind uses to locate (e.g.) the block files consists of the source code build folder,
            // the relative path within the test case folders, and the bitcoind network-specific path to its block database
            // This adds roughly 37 characters onto the full data folder path: \regtest\blocks\index/MANIFEST-000001

            // By throwing here we avoid a pointless 5-minute wait for bitcoind to start up (it will 'start' and then soon
            // crash, which results in the getblockhash RPC call timing out later on in the startup sequence).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (new FileInfo(this.DataFolder).FullName.Length > 222))
                throw new Exception("Path is too long for bitcoind to function.");

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
                failureReason: $"Failed to start BitcoinD within {duration} seconds");
        }

        public override void BuildNode()
        {
        }
    }
}