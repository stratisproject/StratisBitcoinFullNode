using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>Class that checks if federation members fulfill the collateral requirement.</summary>
    public class CollateralChecker : IDisposable
    {
        private readonly BlockStoreClient blockStoreClient;

        private readonly IFederationManager federationManager;

        private readonly ISignals signals;

        private readonly ILogger logger;

        /// <summary>Protects access to <see cref="depositsByFederationMember"/>.</summary>

        private readonly object locker;

        private SubscriptionToken memberAddedToken, memberKickedToken;

        /// <summary>Deposits mapped by federation member.</summary>
        /// <remarks>
        /// Deposits are not updated if federation member doesn't have collateral requirement enabled.
        /// All access should be protected by <see cref="locker"/>.
        /// </remarks>
        private Dictionary<CollateralFederationMember, Money> depositsByFederationMember;

        public CollateralChecker(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, FederationGatewaySettings settings,
            IFederationManager federationManager, ISignals signals)
        {
            this.federationManager = federationManager;
            this.signals = signals;

            this.locker = new object();
            this.depositsByFederationMember = new Dictionary<CollateralFederationMember, Money>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.blockStoreClient = new BlockStoreClient(loggerFactory, httpClientFactory, settings.CounterChainApiPort);
        }

        public void Initialize()
        {
            this.memberAddedToken = this.signals.Subscribe<FedMemberAdded>(this.OnFedMemberAdded);
            this.memberKickedToken = this.signals.Subscribe<FedMemberKicked>(this.OnFedMemberKicked);

            foreach (CollateralFederationMember federationMember in this.federationManager.GetFederationMembers().Cast<CollateralFederationMember>())
                this.depositsByFederationMember.Add(federationMember, null);

            // TODO DO first API update in initialize to get initial values

            // TODO start updating deposits in BG. Ask API only for those that have address and not null amount
        }

        public bool CheckCollateral(IFederationMember federationMember)
        {
            lock (this.locker)
            {
                var member = federationMember as CollateralFederationMember;

                if (!this.depositsByFederationMember.TryGetValue(member, out Money value))
                {
                    this.logger.LogTrace("(-)[NOT_FOUND]");
                    throw new ArgumentException("Provided federation member wasn't found.");
                }

                if (member.CollateralAmount == null)
                {
                    this.logger.LogTrace("(-)[NO_COLLATERAL_REQUIREMENT]:true");
                    return true;
                }

                return value >= member.CollateralAmount;
            }
        }

        private void OnFedMemberKicked(FedMemberKicked fedMemberKicked)
        {
            lock (this.locker)
            {
                this.depositsByFederationMember.Remove((CollateralFederationMember) fedMemberKicked.KickedMember);
            }
        }

        private void OnFedMemberAdded(FedMemberAdded fedMemberAdded)
        {
            lock (this.locker)
            {
                this.depositsByFederationMember.Add((CollateralFederationMember)fedMemberAdded.AddedMember, null);
            }
        }

        public void Dispose()
        {
            this.signals.Unsubscribe(this.memberAddedToken);
            this.signals.Unsubscribe(this.memberKickedToken);

            // TODO stop BG update
        }
    }
}
