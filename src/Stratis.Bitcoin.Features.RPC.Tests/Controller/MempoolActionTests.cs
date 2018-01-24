﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class MempoolActionTests : BaseRPCControllerTest
    {
        [Fact]
        public async Task CanCall_GetRawMempoolAsync()
        {
            var initialBlockSignature = Block.BlockSignature;

            try
            {
                Block.BlockSignature = false;
                string dir = CreateTestDir(this);
                IFullNode fullNode = this.BuildServicedNode(dir);
                MempoolController controller = fullNode.Services.ServiceProvider.GetService<MempoolController>();

                List<uint256> result = await controller.GetRawMempool();

                Assert.NotNull(result);
            }
            finally
            {
                Block.BlockSignature = initialBlockSignature;
            }
        }
    }
}
