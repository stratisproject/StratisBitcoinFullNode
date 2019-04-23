﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg
{
    public class CollateralFederationManager : FederationManagerBase
    {
        public CollateralFederationManager(NodeSettings nodeSettings, Network network, ILoggerFactory loggerFactory, IKeyValueRepository keyValueRepo, ISignals signals)
            : base(nodeSettings, network, loggerFactory, keyValueRepo, signals)
        {
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

        public class CollateralFederationMemberModel
        {
            public string PubKeyHex { get; set; }

            public long CollateralAmountSatoshis { get; set; }

            public string CollateralMainchainAddress { get; set; }
        }
    }
}
