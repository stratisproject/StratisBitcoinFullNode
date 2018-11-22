using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

// TODO create common storage for the data
// TODO add logs

namespace Stratis.Bitcoin.Base
{
    public class TipsManager : IDisposable
    {
        private readonly Dictionary<object, ChainedHeader> tipsByProvider;

        /// <summary>Protects access to <see cref="tipsByProvider"/>.</summary>
        private readonly object lockObject;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public TipsManager(ILoggerFactory loggerFactory)
        {
            this.tipsByProvider = new Dictionary<object, ChainedHeader>();
            this.lockObject = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        // Type is the type of component that requires syncing the tips.
        public void RegisterTipProvider(object provider)
        {
            lock (this.lockObject)
            {
                this.tipsByProvider.Add(provider, null);
            }
        }

        // throws if provider was not registered
        public void CommitTipPersisted(object provider, ChainedHeader tip)
        {
            lock (this.lockObject)
            {
                this.tipsByProvider[provider] = tip;

                // Get lowest tip out of all tips commited.
                ChainedHeader lowestTip = null;
                foreach (ChainedHeader chainedHeader in this.tipsByProvider.Values)
                {
                    // Do nothing if there is at least 1 component that didn't commit it's tip yet.
                    if (chainedHeader == null)
                        return;

                    if ((lowestTip == null) || (chainedHeader.Height < lowestTip.Height))
                        lowestTip = chainedHeader;
                }

                // Last common tip can't be changed because lowest tip is equal to it already.
                if (this.GetLastCommonTip() == lowestTip)
                    return;

                // Make sure all tips are on the same chain.
                foreach (ChainedHeader chainedHeader in this.tipsByProvider.Values)
                {
                    if (chainedHeader.GetAncestor(lowestTip.Height) != lowestTip)
                    {
                        // Not all tips are on the same chain.
                        return;
                    }
                }

                // TODO save in background
            }

            // TODO check if common tip acheived and if it was- push it to queue for saving
        }

        // Returns highest tip commited by all components.
        public ChainedHeader GetLastCommonTip()
        {
            // TODO get cached or load from db
            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
