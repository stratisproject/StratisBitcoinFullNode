using System;
using System.Linq;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    //immutable scoped singleton pattern
    //This singleton allows is to access the name and info of the sidechain in
    //the SBFN Network class without making big changes to SBFN.
    public sealed class SidechainIdentifier : IDisposable
    {
        private static SidechainIdentifier instance = null;
        private static readonly object locker = new object();

        public string Name { get; }

        public ISidechainInfoProvider InfoProvider { get; private set; }

        public static SidechainIdentifier Instance => instance;

        private SidechainIdentifier(string name)
        {
            this.Name = name;
        }

        public static SidechainIdentifier CreateFromArgs(string[] args)
        {
            //expects sidechainName=name
            Func<string, string> lookup =
                option => args.Where(s => s.StartsWith(option)).Select(s => s.Substring(option.Length))
                    .FirstOrDefault();

            string sidechainName = lookup("-sidechainName=");
            string dataDir = lookup("-datadir=");

            if (sidechainName == null)
                throw new ArgumentException("A -sidechainName arg must be specified.");

            return dataDir == null
                ? SidechainIdentifier.Create(sidechainName)
                : SidechainIdentifier.Create(sidechainName, dataDir);
        }

        public static SidechainIdentifier Create(string name)
        {
            var sidechainInfoProvider = new DefaultSidechainInfoProvider();
            return SidechainIdentifier.Create(name, sidechainInfoProvider);
        }

        public static SidechainIdentifier Create(string name, string stratisNodePath)
        {
            var sidechainInfoProvider = new DefaultSidechainInfoProvider(stratisNodePath);
            return SidechainIdentifier.Create(name, sidechainInfoProvider);
        }

        internal static SidechainIdentifier Create(string name, ISidechainInfoProvider sidechainInfoProvider)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid sidechain name.");

            lock (SidechainIdentifier.locker)
            {
                if (SidechainIdentifier.instance != null)
                    throw new InvalidOperationException("SidechainIdentifier is immutable.");

                SidechainIdentifier.instance = new SidechainIdentifier(name);
                SidechainIdentifier.Instance.InfoProvider = sidechainInfoProvider;
            }

            return instance;
        }

        public void Dispose()
        {
            instance = null;
        }
    }
}