﻿using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Common
{
    public class TestPoANetwork : PoANetwork
    {
        public Key FederationKey1 { get; private set; }

        public Key FederationKey2 { get; private set; }

        public Key FederationKey3 { get; private set; }

        public TestPoANetwork()
        {
            this.FederationKey1 = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom").DeriveExtKey().PrivateKey;
            this.FederationKey2 = new Mnemonic("idle power swim wash diesel blouse photo among eager reward govern menu").DeriveExtKey().PrivateKey;
            this.FederationKey3 = new Mnemonic("high neither night category fly wasp inner kitchen phone current skate hair").DeriveExtKey().PrivateKey;

            var genesisFederationMembers = new List<IFederationMember>()
            {
                new FederationMember(this.FederationKey1.PubKey), // 029528e83f065153d7fa655e73a07fc96fc759162f1e2c8936fa592f2942f39af0
                new FederationMember(this.FederationKey2.PubKey), // 03b539807c64abafb2d14c52a0d1858cc29d7c7fad0598f92a1274789c18d74d2d
                new FederationMember(this.FederationKey3.PubKey)  // 02d6792cf941b68edd1e9056653573917cbaf974d46e9eeb9801d6fcedf846477a
            };

            var baseOptions = this.Consensus.Options as PoAConsensusOptions;

            this.Consensus.Options = new PoAConsensusOptions(
                maxBlockBaseSize: baseOptions.MaxBlockBaseSize,
                maxStandardVersion: baseOptions.MaxStandardVersion,
                maxStandardTxWeight: baseOptions.MaxStandardTxWeight,
                maxBlockSigopsCost: baseOptions.MaxBlockSigopsCost,
                maxStandardTxSigopsCost: baseOptions.MaxStandardTxSigopsCost,
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 60,
                votingEnabled: baseOptions.VotingEnabled,
                autoKickIdleMembers: false,
                federationMemberMaxIdleTimeSeconds: baseOptions.FederationMemberMaxIdleTimeSeconds
            );

            this.Consensus.SetPrivatePropertyValue(nameof(this.Consensus.MaxReorgLength), (uint)5);
        }
    }
}
