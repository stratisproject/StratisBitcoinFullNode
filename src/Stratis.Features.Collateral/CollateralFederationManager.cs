using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Collateral
{
    public class CollateralFederationManager : FederationManagerBase
    {
        public CollateralFederationManager(NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo, ISignals signals)
            : base(nodeSettings, network, loggerFactory, keyValueRepo, signals)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            IEnumerable<CollateralFederationMember> collateralMembers = this.federationMembers.Cast<CollateralFederationMember>().Where(x => x.CollateralAmount != null && x.CollateralAmount > 0);

            if (collateralMembers.Any(x => x.CollateralMainchainAddress == null))
            {
                throw new Exception("Federation can't contain members with non-zero collateral requirement but null collateral address.");
            }

            int distinctCount = collateralMembers.Select(x => x.CollateralMainchainAddress).Distinct().Count();

            if (distinctCount != collateralMembers.Count())
            {
                throw new Exception("Federation can't contain members with duplicated collateral addresses.");
            }
        }

        protected override void AddFederationMemberLocked(IFederationMember federationMember)
        {
            var collateralMember = federationMember as CollateralFederationMember;

            if (this.federationMembers.Cast<CollateralFederationMember>().Any(x => x.CollateralMainchainAddress == collateralMember.CollateralMainchainAddress))
            {
                this.logger.LogTrace("(-)[DUPLICATED_COLLATERAL_ADDR]");
                return;
            }

            base.AddFederationMemberLocked(federationMember);
        }

        protected override List<IFederationMember> LoadFederation()
        {
            List<CollateralFederationMemberModel> fedMemberModels = this.keyValueRepo.LoadValueJson<List<CollateralFederationMemberModel>>(federationMembersDbKey);

            if (fedMemberModels == null)
            {
                this.logger.LogTrace("(-)[NOT_FOUND]:null");
                return null;
            }

            var federation = new List<IFederationMember>(fedMemberModels.Count);

            foreach (CollateralFederationMemberModel fedMemberModel in fedMemberModels)
            {
                federation.Add(new CollateralFederationMember(new PubKey(fedMemberModel.PubKeyHex), new Money(fedMemberModel.CollateralAmountSatoshis),
                    fedMemberModel.CollateralMainchainAddress));
            }

            return federation;
        }

        protected override void SaveFederation(List<IFederationMember> federation)
        {
            IEnumerable<CollateralFederationMember> collateralFederation = federation.Cast<CollateralFederationMember>();

            var modelsCollection = new List<CollateralFederationMemberModel>(federation.Count);

            foreach (CollateralFederationMember federationMember in collateralFederation)
            {
                modelsCollection.Add(new CollateralFederationMemberModel()
                {
                    PubKeyHex = federationMember.PubKey.ToHex(),
                    CollateralMainchainAddress = federationMember.CollateralMainchainAddress,
                    CollateralAmountSatoshis = federationMember.CollateralAmount.Satoshi
                });
            }

            this.keyValueRepo.SaveValueJson(federationMembersDbKey, modelsCollection);
        }
    }
}
