using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class RPCControllerTest : LogsTestBase
    {
        private readonly Network testNetwork;
        private readonly Mock<IFullNode> fullNode;
        private readonly RpcSettings rpcSettings;
        private readonly RPCController controller;
        private readonly Mock<IRPCClientFactory> rpcClientFactory;
        private readonly Mock<IWebHost> rpcHost;
        private readonly Mock<IServiceProvider> serviceProvider;
        private readonly Mock<IActionDescriptorCollectionProvider> actionDescriptorCollectionProvider;
        private readonly Mock<IRPCClient> rpcClient;
        private readonly List<ActionDescriptor> descriptors;

        public RPCControllerTest()
        {
            this.testNetwork = KnownNetworks.TestNet;
            this.fullNode = new Mock<IFullNode>();
            this.fullNode.Setup(f => f.Network)
                .Returns(this.testNetwork);
            this.rpcHost = new Mock<IWebHost>();
            var nodeSettings = new NodeSettings(this.testNetwork, args: new string[] { "-server=1" });
            this.rpcSettings = new RpcSettings(nodeSettings);
            this.serviceProvider = new Mock<IServiceProvider>();
            this.rpcClientFactory = new Mock<IRPCClientFactory>();
            this.actionDescriptorCollectionProvider = new Mock<IActionDescriptorCollectionProvider>();

            this.rpcClient = new Mock<IRPCClient>();
            this.rpcSettings.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0));
            this.rpcClientFactory.Setup(r => r.Create($"{this.rpcSettings.RpcUser}:{this.rpcSettings.RpcPassword}", It.Is<Uri>(u => u.ToString() == "http://127.0.0.1:0/"), It.IsAny<Network>()))
                .Returns(this.rpcClient.Object);

            this.fullNode.Setup(f => f.RPCHost)
                .Returns(this.rpcHost.Object);
            this.rpcHost.Setup(c => c.Services)
                .Returns(this.serviceProvider.Object);
            this.serviceProvider.Setup(s => s.GetService(typeof(IActionDescriptorCollectionProvider)))
                .Returns(this.actionDescriptorCollectionProvider.Object);

            this.descriptors = new List<ActionDescriptor>();
            this.actionDescriptorCollectionProvider.Setup(a => a.ActionDescriptors)
                .Returns(() =>
                {
                    return new ActionDescriptorCollection(this.descriptors, 0);
                });

            this.controller = new RPCController(this.fullNode.Object, this.LoggerFactory.Object, this.rpcSettings, this.rpcClientFactory.Object);
        }

        [Fact]
        public void ListMethods_WithDescriptors_ListsRpcMethods()
        {
            var descriptor = new ControllerActionDescriptor()
            {
                ActionName = "getblockheader",
                MethodInfo = typeof(FullNodeController).GetMethod("GetBlockHeader"),
                Parameters = new List<ParameterDescriptor>()
            };

            foreach (ParameterInfo parameter in typeof(FullNodeController).GetMethod("GetBlockHeader").GetParameters())
            {
                descriptor.Parameters.Add(new ControllerParameterDescriptor()
                {
                    Name = parameter.Name,
                    ParameterType = parameter.ParameterType,
                    ParameterInfo = parameter
                });
            }
            this.descriptors.Add(descriptor);

            IActionResult controllerResult = this.controller.ListMethods();

            var jsonResult = Assert.IsType<JsonResult>(controllerResult);
            var model = Assert.IsType<List<RpcCommandModel>>(jsonResult.Value);
            Assert.NotEmpty(model);
            RpcCommandModel commandModel = model[0];
            Assert.Equal("getblockheader <hash> [<isjsonformat>]", commandModel.Command);
            Assert.Equal("Gets the block header of the block identified by the hash.", commandModel.Description);
        }

        [Fact]
        public void ListMethods_NoDescriptors_ReturnsEmptyModel()
        {
            IActionResult controllerResult = this.controller.ListMethods();

            var jsonResult = Assert.IsType<JsonResult>(controllerResult);
            var model = Assert.IsType<List<RpcCommandModel>>(jsonResult.Value);
            Assert.Empty(model);
        }


        [Fact]
        public void ListMethods_WithException_ReturnsErrorResult()
        {
            this.fullNode.Setup(f => f.RPCHost)
                .Throws(new InvalidOperationException("Could not find RPCHost"));

            IActionResult controllerResult = this.controller.ListMethods();

            var errorResult = Assert.IsType<ErrorResult>(controllerResult);
            Assert.Equal(400, errorResult.StatusCode);
            var errorValue = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal("Could not find RPCHost", errorValue.Errors[0].Message);
            Assert.Equal(400, errorValue.Errors[0].Status);
        }

        [Fact]
        public void CallByName_CorrectQueryString_SendsRpcRequest_ReturnsResult()
        {
            //setup
            var descriptor = new ControllerActionDescriptor()
            {
                ActionName = "getblockheader",
                MethodInfo = typeof(FullNodeController).GetMethod("GetBlockHeader"),
                Parameters = new List<ParameterDescriptor>()
            };

            foreach (ParameterInfo parameter in typeof(FullNodeController).GetMethod("GetBlockHeader").GetParameters())
            {
                descriptor.Parameters.Add(new ControllerParameterDescriptor()
                {
                    Name = parameter.Name,
                    ParameterType = parameter.ParameterType,
                    ParameterInfo = parameter
                });
            }
            this.descriptors.Add(descriptor);

            this.controller.ControllerContext = new ControllerContext();
            this.controller.ControllerContext.HttpContext = new DefaultHttpContext();
            
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new RPCResponseObject()))))
            {
                RPCResponse rpcResponse = RPCResponse.Load(stream);
                this.rpcClient.Setup(c => c.SendCommand(It.Is<RPCRequest>(r => r.Method == "getblockheader"
                                                        && ((string)r.Params[0]) == new uint256(1000).ToString()
                                                        && ((string)r.Params[1]) == "true"), true))
                    .Returns(rpcResponse)
                    .Verifiable();

                // call
                var body = JObject.FromObject(new { methodName = "getblockheader", hash = new uint256(1000).ToString(), isjsonformat = "true" });
                IActionResult controllerResult = this.controller.CallByName(body);

                //verify
                this.rpcClient.Verify();
                var jsonResult = Assert.IsType<JsonResult>(controllerResult);
                var result = jsonResult.Value as JToken;
                Assert.Equal(this.testNetwork.GenesisHash.ToString(), result["hashPrevBlock"].ToString());
            }
        }

        [Fact]
        public void CallByName_RpcMethodNotFound_ReturnsErrorResult()
        {
            var values = new Dictionary<string, StringValues>();
            values.Add("hash", new StringValues(new uint256(1000).ToString()));
            values.Add("isjsonformat", new StringValues("true"));
            this.controller.ControllerContext = new ControllerContext();
            this.controller.ControllerContext.HttpContext = new DefaultHttpContext();
            this.controller.ControllerContext.HttpContext.Request.Query = new QueryCollection(values);

            var body = JObject.FromObject(new {methodName = "getblockheader"});
            IActionResult controllerResult = this.controller.CallByName(body);

            var errorResult = Assert.IsType<ErrorResult>(controllerResult);
            Assert.Equal(400, errorResult.StatusCode);
            var errorValue = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal("RPC method 'getblockheader' not found.", errorValue.Errors[0].Message);
            Assert.Equal(400, errorValue.Errors[0].Status);
        }

        [Fact]
        public void CallByName_WithException_ReturnsErrorResult()
        {
            this.fullNode.Setup(f => f.RPCHost)
               .Throws(new InvalidOperationException("Could not find RPCHost"));

            var body = JObject.FromObject(new { methodName = "getblockheader" });
            IActionResult controllerResult = this.controller.CallByName(body);

            var errorResult = Assert.IsType<ErrorResult>(controllerResult);
            Assert.Equal(400, errorResult.StatusCode);
            var errorValue = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Equal("Could not find RPCHost", errorValue.Errors[0].Message);
            Assert.Equal(400, errorValue.Errors[0].Status);
        }

        private class RPCResponseObject
        {
            public BlockHeaderObject result = new BlockHeaderObject()
            {
                hashPrevBlock = KnownNetworks.TestNet.GenesisHash.ToString()
            };

            public RPCErrorObject error = new RPCErrorObject()
            {
                code = 0,
                message = null
            };
        }

        private class BlockHeaderObject
        {
            public string hashPrevBlock { get; set; }
        }

        private class RPCErrorObject
        {
            public int code { get; set; }

            public string message { get; set; }
        }
    }
}
