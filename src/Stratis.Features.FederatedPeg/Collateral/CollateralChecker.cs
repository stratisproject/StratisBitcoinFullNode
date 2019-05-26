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

        private bool isInitialized;

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
            this.isInitialized = false;
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

            while (true)
            {
                bool success = await this.UpdateCollateralInfoAsync(this.cancellationSource.Token).ConfigureAwait(false);

                if (!success)
                {
                    this.logger.LogWarning("Failed to update collateral. Ensure that mainnet gateway node is running and API is enabled. " +
                                       "Node will not continue initialization before another gateway node responds.");

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
            this.isInitialized = true;
        }

        /// <summary>Continuously updates info about money deposited to fed member's addresses.</summary>
        private async Task UpdateCollateralInfoContinuouslyAsync()
        {
            while (!this.cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CollateralUpdateIntervalSeconds * 1000, this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
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

            this.logger.LogDebug("Addresses to check {0}.", addressesToCheck.Count);

            AddressBalancesModel collateral = await this.blockStoreClient.GetAddressBalancesAsync(addressesToCheck, RequiredConfirmations, cancellation).ConfigureAwait(false);

            if (collateral == null)
            {
                this.logger.LogWarning("Failed to fetch address balances from counter chain node!");
                this.logger.LogTrace("(-)[FAILED]:false");
                return false;
            }

            this.logger.LogDebug("Addresses received {0}.", collateral.Balances.Count);

            if (collateral.Balances.Count != addressesToCheck.Count)
            {
                this.logger.LogDebug("Expected {0} data entries but received {1}.", addressesToCheck.Count, collateral.Balances.Count);

                this.logger.LogTrace("(-)[INCONSISTENT_DATA]:false");
                return false;
            }

            lock (this.locker)
            {
                foreach (AddressBalanceModel addressMoney in collateral.Balances)
                {
                    this.logger.LogDebug("Updating federated member {0} with amount {1}.", addressMoney.Address, addressMoney.Balance);
                    this.depositsByAddress[addressMoney.Address] = addressMoney.Balance;
                }
            }

            return true;
        }

        public bool CheckCollateral(IFederationMember federationMember)
        {
            if (!this.isInitialized)
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
