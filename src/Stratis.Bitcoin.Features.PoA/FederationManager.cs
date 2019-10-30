using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public interface IFederationManager
    {
        /// <summary><c>true</c> in case current node is a federation member.</summary>
        bool IsFederationMember { get; }

        /// <summary>Current federation member's private key. <c>null</c> if <see cref="IsFederationMember"/> is <c>false</c>.</summary>
        Key CurrentFederationKey { get; }

        void Initialize();

        /// <summary>Provides up to date list of federation members.</summary>
        /// <remarks>
        /// Blocks that are not signed with private keys that correspond
        /// to public keys from this list are considered to be invalid.
        /// </remarks>
        List<IFederationMember> GetFederationMembers();

        void AddFederationMember(IFederationMember federationMember);

        void RemoveFederationMember(IFederationMember federationMember);

        /// <summary>Provides federation member of this node or <c>null</c> if <see cref="IsFederationMember"/> is <c>false</c>.</summary>
        IFederationMember GetCurrentFederationMember();
    }

    public abstract class FederationManagerBase : IFederationManager
    {
        /// <inheritdoc />
        public bool IsFederationMember { get; private set; }

        /// <inheritdoc />
        public Key CurrentFederationKey { get; private set; }

        protected readonly IKeyValueRepository keyValueRepo;

        protected readonly ILogger logger;

        private readonly NodeSettings settings;

        private readonly PoANetwork network;

        private readonly ISignals signals;

        /// <summary>Key for accessing list of public keys that represent federation members from <see cref="IKeyValueRepository"/>.</summary>
        protected const string federationMembersDbKey = "fedmemberskeys";

        /// <summary>Collection of all active federation members.</summary>
        /// <remarks>All access should be protected by <see cref="locker"/>.</remarks>
        protected List<IFederationMember> federationMembers;

        /// <summary>Protects access to <see cref="federationMembers"/>.</summary>
        protected readonly object locker;

        public FederationManagerBase(NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo, ISignals signals)
        {
            this.settings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.network = Guard.NotNull(network as PoANetwork, nameof(network));
            this.keyValueRepo = Guard.NotNull(keyValueRepo, nameof(keyValueRepo));
            this.signals = Guard.NotNull(signals, nameof(signals));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.locker = new object();
        }

        public virtual void Initialize()
        {
            // Load federation from the db.
            this.federationMembers = this.LoadFederation();

            if (this.federationMembers == null)
            {
                this.logger.LogDebug("Federation members are not stored in the db. Loading genesis federation members.");

                this.federationMembers = new List<IFederationMember>(this.network.ConsensusOptions.GenesisFederationMembers);

                this.SaveFederation(this.federationMembers);
            }

            // Display federation.
            this.logger.LogInformation("Federation contains {0} members. Their public keys are: {1}",
                this.federationMembers.Count, Environment.NewLine + string.Join(Environment.NewLine, this.federationMembers));

            // Load key.
            Key key = new KeyTool(this.settings.DataFolder).LoadPrivateKey();

            this.CurrentFederationKey = key;
            this.SetIsFederationMember();

            if (this.CurrentFederationKey == null)
            {
                this.logger.LogTrace("(-)[NOT_FED_MEMBER]");
                return;
            }

            // Loaded key has to be a key for current federation.
            if (!this.federationMembers.Any(x => x.PubKey == this.CurrentFederationKey.PubKey))
            {
                string message = "Key provided is not registered on the network!";

                this.logger.LogWarning(message);
            }

            this.logger.LogInformation("Federation key pair was successfully loaded. Your public key is: '{0}'.", this.CurrentFederationKey.PubKey);
        }

        private void SetIsFederationMember()
        {
            this.IsFederationMember = this.federationMembers.Any(x => x.PubKey == this.CurrentFederationKey?.PubKey);
        }

        /// <inheritdoc />
        public List<IFederationMember> GetFederationMembers()
        {
            lock (this.locker)
            {
                return new List<IFederationMember>(this.federationMembers);
            }
        }

        /// <inheritdoc />
        public IFederationMember GetCurrentFederationMember()
        {
            lock (this.locker)
            {
                return this.federationMembers.SingleOrDefault(x => x.PubKey == this.CurrentFederationKey.PubKey);
            }
        }

        public void AddFederationMember(IFederationMember federationMember)
        {
            lock (this.locker)
            {
                this.AddFederationMemberLocked(federationMember);
            }

            this.signals.Publish(new FedMemberAdded(federationMember));
        }

        /// <remarks>Should be protected by <see cref="locker"/>.</remarks>
        protected virtual void AddFederationMemberLocked(IFederationMember federationMember)
        {
            if (this.federationMembers.Contains(federationMember))
            {
                this.logger.LogTrace("(-)[ALREADY_EXISTS]");
                return;
            }

            this.federationMembers.Add(federationMember);

            this.SaveFederation(this.federationMembers);
            this.SetIsFederationMember();

            this.logger.LogInformation("Federation member '{0}' was added!", federationMember);
        }

        public void RemoveFederationMember(IFederationMember federationMember)
        {
            lock (this.locker)
            {
                this.federationMembers.Remove(federationMember);

                this.SaveFederation(this.federationMembers);
                this.SetIsFederationMember();

                this.logger.LogInformation("Federation member '{0}' was removed!", federationMember);
            }

            this.signals.Publish(new FedMemberKicked(federationMember));
        }

        protected abstract void SaveFederation(List<IFederationMember> federation);

        /// <summary>Loads saved collection of federation members from the database.</summary>
        protected abstract List<IFederationMember> LoadFederation();
    }

    public class FederationManager : FederationManagerBase
    {
        public FederationManager(NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo, ISignals signals)
            :base(nodeSettings, network, loggerFactory, keyValueRepo, signals)
        {
        }

        protected override void SaveFederation(List<IFederationMember> federation)
        {
            List<string> hexList = federation.Select(x => x.PubKey.ToHex()).ToList();

            this.keyValueRepo.SaveValueJson(federationMembersDbKey, hexList);
        }

        /// <inheritdoc />
        protected override List<IFederationMember> LoadFederation()
        {
            List<string> hexList = this.keyValueRepo.LoadValueJson<List<string>>(federationMembersDbKey);

            List<PubKey> keys = hexList?.Select(x => new PubKey(x)).ToList();

            if (keys == null)
            {
                this.logger.LogTrace("(-)[NOT_FOUND]:null");
                return null;
            }

            var loadedFederation = new List<IFederationMember>(keys.Count);

            foreach (PubKey key in keys)
                loadedFederation.Add(new FederationMember(key));

            return loadedFederation;
        }
    }
}
