using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.Collateral
{
    /// <summary>Class that checks if federation members fulfill the collateral requirement.</summary>
    public interface ICollateralChecker : IDisposable
    {
        Task InitializeAsync();

        /// <summary>Checks if given federation member fulfills the collateral requirement.</summary>
        /// <param name="federationMember">The federation member whose collateral will be checked.</param>
        bool CheckCollateral(IFederationMember federationMember);
    }

    public class CollateralChecker : ICollateralChecker
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
        private readonly Dictionary<string, Money> depositsByAddress;

        private Task updateCollateralContinuouslyTask;

        private bool collateralUpdated;

        public CollateralChecker(ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ICounterChainSettings settings,
            IFederationManager federationManager,
            ISignals signals)
        {
            this.federationManager = federationManager;
            this.signals = signals;

            this.cancellationSource = new CancellationTokenSource();
            this.locker = new object();
            this.depositsByAddress = new Dictionary<string, Money>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.blockStoreClient = new BlockStoreClient(loggerFactory, httpClientFactory, $"http://{settings.CounterChainApiHost}", settings.CounterChainApiPort);
        }

        public async Task InitializeAsync()
        {
            this.memberAddedToken = this.signals.Subscribe<FedMemberAdded>(this.OnFedMemberAdded);
            this.memberKickedToken = this.signals.Subscribe<FedMemberKicked>(this.OnFedMemberKicked);

            foreach (CollateralFederationMember federationMember in this.federationManager.GetFederationMembers()
                .Cast<CollateralFederationMember>().Where(x => x.CollateralAmount != null && x.CollateralAmount > 0))
            {
                this.logger.LogDebug("Initializing federation member {0} with amount {1}.", federationMember.CollateralMainchainAddress, federationMember.CollateralAmount);
                this.depositsByAddress.Add(federationMember.CollateralMainchainAddress, 0);
            }

            while (!this.cancellationSource.IsCancellationRequested)
            {
                await this.UpdateCollateralInfoAsync(this.cancellationSource.Token).ConfigureAwait(false);

                if (this.collateralUpdated)
                    break;

                this.logger.LogWarning("Node initialization will not continue until the gateway node responds.");
                await this.DelayCollateralCheckAsync().ConfigureAwait(false);
            }

            this.updateCollateralContinuouslyTask = this.UpdateCollateralInfoContinuouslyAsync();
        }

        /// <summary>Continuously updates info about money deposited to fed member's addresses.</summary>
        private async Task UpdateCollateralInfoContinuouslyAsync()
        {
            while (!this.cancellationSource.IsCancellationRequested)
            {
                await this.UpdateCollateralInfoAsync(this.cancellationSource.Token).ConfigureAwait(false);

                await this.DelayCollateralCheckAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Delay checking the federation member's collateral with <see cref="CollateralUpdateIntervalSeconds"/> seconds.
        /// </summary>
        private async Task DelayCollateralCheckAsync()
        {
            try
            {
                await Task.Delay(CollateralUpdateIntervalSeconds * 1000, this.cancellationSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELLED]");
            }
        }

        private async Task UpdateCollateralInfoAsync(CancellationToken cancellation)
        {
            List<string> addressesToCheck;

            lock (this.locker)
            {
                addressesToCheck = this.depositsByAddress.Keys.ToList();
            }

            if (addressesToCheck.Count == 0)
            {
                this.collateralUpdated = true;

                this.logger.LogInformation("None of the federation members has a collateral requirement configured.");
                this.logger.LogTrace("(-)[NOTHING_TO_CHECK]:true");
                return;
            }

            this.logger.LogDebug("Addresses to check {0}.", addressesToCheck.Count);

            AddressBalancesResult addressBalanceResult = await this.blockStoreClient.GetAddressBalancesAsync(addressesToCheck, RequiredConfirmations, cancellation).ConfigureAwait(false);

            if (addressBalanceResult == null)
            {
                this.logger.LogWarning("Failed to update collateral, please ensure that the mainnet gateway node is running and it's API feature is enabled.");
                this.logger.LogTrace("(-)[CALL_RETURNED_NULL_RESULT]:false");
                return;
            }

            if (!string.IsNullOrEmpty(addressBalanceResult.Reason))
            {
                this.logger.LogWarning("Failed to fetch address balances from the counter chain node : {0}", addressBalanceResult.Reason);
                this.logger.LogTrace("(-)[FAILED]:{0}", addressBalanceResult.Reason);
                return;
            }

            this.logger.LogDebug("Addresses received {0}.", addressBalanceResult.Balances.Count);

            if (addressBalanceResult.Balances.Count != addressesToCheck.Count)
            {
                this.logger.LogDebug("Expected {0} data entries but received {1}.", addressesToCheck.Count, addressBalanceResult.Balances.Count);
                this.logger.LogTrace("(-)[CALL_RETURNED_INCONSISTENT_DATA]:false");
                return;
            }

            lock (this.locker)
            {
                foreach (AddressBalanceResult addressMoney in addressBalanceResult.Balances)
                {
                    this.logger.LogDebug("Updating federation member {0} with amount {1}.", addressMoney.Address, addressMoney.Balance);
                    this.depositsByAddress[addressMoney.Address] = addressMoney.Balance;
                }
            }

            this.collateralUpdated = true;
        }

        /// <inheritdoc />
        public bool CheckCollateral(IFederationMember federationMember)
        {
            if (!this.collateralUpdated)
            {
                this.logger.LogTrace("(-)[NOT_INITIALIZED]");
                throw new Exception("Component is not initialized!");
            }

            var member = federationMember as CollateralFederationMember;

            if (member == null)
            {
                this.logger.LogTrace("(-)[WRONG_TYPE]");
                throw new ArgumentException($"{nameof(federationMember)} should be of type: {nameof(CollateralFederationMember)}.");
            }

            if ((member.CollateralAmount == null) || (member.CollateralAmount == 0))
            {
                this.logger.LogTrace("(-)[NO_COLLATERAL_REQUIREMENT]:true");
                return true;
            }

            lock (this.locker)
            {
                return (this.depositsByAddress[member.CollateralMainchainAddress] ?? 0) >= member.CollateralAmount;
            }
        }

        private void OnFedMemberKicked(FedMemberKicked fedMemberKicked)
        {
            lock (this.locker)
            {
                this.logger.LogDebug("Removing federation member {0}", ((CollateralFederationMember)fedMemberKicked.KickedMember).CollateralMainchainAddress);
                this.depositsByAddress.Remove(((CollateralFederationMember)fedMemberKicked.KickedMember).CollateralMainchainAddress);
            }
        }

        private void OnFedMemberAdded(FedMemberAdded fedMemberAdded)
        {
            lock (this.locker)
            {
                this.logger.LogDebug("Adding federation member {0}", ((CollateralFederationMember)fedMemberAdded.AddedMember).CollateralMainchainAddress);
                this.depositsByAddress.Add(((CollateralFederationMember)fedMemberAdded.AddedMember).CollateralMainchainAddress, 0);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.signals.Unsubscribe(this.memberAddedToken);
            this.signals.Unsubscribe(this.memberKickedToken);

            this.cancellationSource.Cancel();

            this.updateCollateralContinuouslyTask?.GetAwaiter().GetResult();
        }
    }
}
