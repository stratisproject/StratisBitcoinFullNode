using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisXRunner : NodeRunner
    {
        private readonly string stratisDPath;
        private Process process;

        public StratisXRunner(string dataDir, string stratisDPath) : base(dataDir, null)
        {
            this.stratisDPath = stratisDPath;
            this.Network = new StratisRegTest();
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
            TimeSpan duration = TimeSpan.FromSeconds(15);

            // The complete path stratisd uses to locate (e.g.) the block files consists of the source code build folder,
            // the relative path within the test case folders, and the stratisd network-specific path to its block database
            // This adds roughly 37 characters onto the full data folder path: \regtest\blocks\index/MANIFEST-000001

            // By throwing here we avoid a pointless 5-minute wait for stratisd to start up (it will 'start' and then soon
            // crash, which results in the getblockhash RPC call timing out later on in the startup sequence).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (new FileInfo(this.DataFolder).FullName.Length > 222))
                throw new Exception("Path is too long for stratisd to function.");

            TestHelper.WaitLoop(() =>
            {
                try
                {
                    this.process = Process.Start(new FileInfo(this.stratisDPath).FullName,
                        $"-conf=stratis.conf -datadir={this.DataFolder}");
                    return true;
                }
                catch
                {
                    return false;
                }
            }, cancellationToken: new CancellationTokenSource(duration).Token,
                failureReason: $"Failed to start StratisD within {duration} seconds");
        }

        public override void BuildNode()
        {
        }
    }
}