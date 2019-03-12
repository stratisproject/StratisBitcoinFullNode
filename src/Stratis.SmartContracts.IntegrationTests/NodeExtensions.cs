﻿using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.Tests.Common.MockChain;

namespace Stratis.SmartContracts.IntegrationTests
{
    public static class NodeExtensions
    {
        public static void WhitelistCode(this IMockChain chain, byte[] code)
        {
            foreach (var node in chain.Nodes)
            {
                var hasher = node.CoreNode.FullNode.NodeService<IContractCodeHashingStrategy>();
                var hash = new uint256(hasher.Hash(code));
                node.CoreNode.FullNode.NodeService<IWhitelistedHashesRepository>().AddHash(hash);
            }
        }

        public static List<uint256> GetWhitelistedHashes(this CoreNode node)
        {
            return node.FullNode.NodeService<IWhitelistedHashesRepository>().GetHashes();
        }
    }
}