using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Stratis.SmartContracts.Core.Deployment;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Deployer
{
    public class HttpContractDeployerTests
    {
        [Fact]
        public async Task SmartContracts_HttpDeployContract_Success()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();
            var contractAddress = "Test";
            var successMessage = "Contract was successfully deployed";

            var response = new BuildCreateContractTransactionResponse
            {
                NewContractAddress = contractAddress,
                Success = true,
                Message = successMessage
            };

            mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(NetJSON.NetJSON.Serialize(response))
                }));

            var httpContractDeployer = new HttpContractDeployer(new HttpClient(mockMessageHandler.Object));
            
            DeploymentResult result = await httpContractDeployer.DeployAsync("http://test", new BuildCreateContractTransactionRequest());

            Assert.True(result.Success);
            Assert.Equal(contractAddress, result.ContractAddress);
            Assert.Equal(successMessage, response.Message);
        }

        [Fact]
        public async Task SmartContracts_HttpDeployContract_Fail()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>(); 

            mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                }));

            var httpContractDeployer = new HttpContractDeployer(new HttpClient(mockMessageHandler.Object));

            DeploymentResult result = await httpContractDeployer.DeployAsync("http://test", new BuildCreateContractTransactionRequest());

            Assert.False(result.Success);
            Assert.Null(result.ContractAddress);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task SmartContracts_HttpDeployContract_Timeout()
        {
            var mockMessageHandler = new Mock<HttpMessageHandler>();

            mockMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Throws<HttpRequestException>();

            var httpContractDeployer = new HttpContractDeployer(new HttpClient(mockMessageHandler.Object));

            DeploymentResult result = await httpContractDeployer.DeployAsync("http://test", new BuildCreateContractTransactionRequest());

            Assert.False(result.Success);
            Assert.Null(result.ContractAddress);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void SmartContracts_HttpDeployer_BuildsCorrectUri()
        {
            Uri uri = HttpContractDeployer.GetDeploymentUri("http://test/", "/api/Tester");

            Assert.Equal("http://test/api/Tester", uri.ToString());
        }
    }
}
