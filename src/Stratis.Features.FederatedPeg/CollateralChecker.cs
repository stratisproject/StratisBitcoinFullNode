using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IBlockStoreClient blockStoreClient;

        private readonly IFederationManager federationManager;

        private readonly ISignals signals;

        private readonly ILogger logger;

        /// <summary>Protects access to <see cref="depositsByAddress"/>.</summary>

        private readonly object locker;

        private readonly CancellationTokenSource cancellationSource;

        private SubscriptionToken memberAddedToken, memberKickedToken;

        /// <summary>Amount of confirmations required for collateral.</summary>
        private const int RequiredConfirmations = 1;

        private const int CollateralInitializationUpdateIntervalSeconds = 3;

        private const int CollateralUpdateIntervalSeconds = 20;

        /// <summary>Deposits mapped by federation member.</summary>
        /// <remarks>
        /// Deposits are not updated if federation member doesn't have collateral requirement enabled.
        /// All access should be protected by <see cref="locker"/>.
        /// </remarks>
        private Dictionary<string, Money> depositsByAddress;

        private Task updateCollateralContinuouslyTask;

        public CollateralChecker(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, FederationGatewaySettings settings,
            IFederationManager federationManager, ISignals signals)
        {
            this.federationManager = federationManager;
            this.signals = signals;

            this.cancellationSource = new CancellationTokenSource();
            this.locker = new object();
            this.depositsByAddress = new Dictionary<string, Money>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.blockStoreClient = new BlockStoreClient(loggerFactory, httpClientFactory, settings.CounterChainApiPort);
        }

        public async Task InitializeAsync()
        {
            this.memberAddedToken = this.signals.Subscribe<FedMemberAdded>(this.OnFedMemberAdded);
            this.memberKickedToken = this.signals.Subscribe<FedMemberKicked>(this.OnFedMemberKicked);

            foreach (CollateralFederationMember federationMember in this.federationManager.GetFederationMembers().Cast<CollateralFederationMember>().Where(x => x.CollateralAmount != null && x.CollateralAmount > 0))
                this.depositsByAddress.Add(federationMember.CollateralMainchainAddress, null);

            while (true)
            {
                bool success = await this.UpdateCollateralInfoAsync(this.cancellationSource.Token).ConfigureAwait(false);

                this.logger.LogWarning("Failed to update collateral. Ensure that mainnet gateway node is running and API is enabled. " +
                                       "Node will not continue initialization before another gateway node responds.");

                if (!success)
                {
                    try
                    {
                        await Task.Delay(CollateralInitializationUpdateIntervalSeconds * 1000, this.cancellationSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        this.logger.LogTrace("(-)[CANCELLED]");
                        return;
                    }
                }
                else
                {
                    break;
                }
            }

            this.updateCollateralContinuouslyTask = this.UpdateCollateralInfoContinuouslyAsync();
        }

        private async Task UpdateCollateralInfoContinuouslyAsync()
        {
            while (!this.cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CollateralUpdateIntervalSeconds * 1000, this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException )
                {
                    this.logger.LogTrace("(-)[CANCELLED]");
                    return;
                }

                bool success = await this.UpdateCollateralInfoAsync(this.cancellationSource.Token).ConfigureAwait(false);

                if (!success)
                    this.logger.LogWarning("Failed to update collateral. Ensure that mainnet gateway node is running and API is enabled.");
            }
        }

        private async Task<bool> UpdateCollateralInfoAsync(CancellationToken cancellation = default(CancellationToken))
        {
            List<string> addressesToCheck;

            lock (this.locker)
            {
                addressesToCheck = this.depositsByAddress.Keys.ToList();
            }

            if (addressesToCheck.Count == 0)
            {
                this.logger.LogTrace("(-)[NOTHING_TO_CHECK]:true");
                return true;
            }

            Dictionary<string, Money> collateral = await this.blockStoreClient.GetAddressBalancesAsync(addressesToCheck, RequiredConfirmations, cancellation).ConfigureAwait(false);

            if (collateral == null)
            {
                this.logger.LogTrace("(-)[FAILED]:false");
                return false;
            }

            if (collateral.Count != addressesToCheck.Count)
            {
                this.logger.LogTrace("(-)[INCONSISTENT_DATA]:false");
                return false;
            }

            lock (this.locker)
            {
                foreach (KeyValuePair<string, Money> addressMoney in collateral)
                    this.depositsByAddress[addressMoney.Key] = addressMoney.Value;
            }

            return true;
        }

        public bool CheckCollateral(IFederationMember federationMember)
        {
            var member = federationMember as CollateralFederationMember;

            if ((member.CollateralAmount == null) || (member.CollateralAmount == 0))
            {
                this.logger.LogTrace("(-)[NO_COLLATERAL_REQUIREMENT]:true");
                return true;
            }

            lock (this.locker)
            {
                return this.depositsByAddress[member.CollateralMainchainAddress] >= member.CollateralAmount;
            }
        }

        private void OnFedMemberKicked(FedMemberKicked fedMemberKicked)
        {
            lock (this.locker)
            {
                this.depositsByAddress.Remove(((CollateralFederationMember) fedMemberKicked.KickedMember).CollateralMainchainAddress);
            }
        }

        private void OnFedMemberAdded(FedMemberAdded fedMemberAdded)
        {
            lock (this.locker)
            {
                this.depositsByAddress.Add(((CollateralFederationMember)fedMemberAdded.AddedMember).CollateralMainchainAddress, null);
            }
        }

        public void Dispose()
        {
            this.signals.Unsubscribe(this.memberAddedToken);
            this.signals.Unsubscribe(this.memberKickedToken);

            this.cancellationSource.Cancel();

            this.updateCollateralContinuouslyTask?.GetAwaiter().GetResult();
        }
    }
}
