using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.SmartContracts.Core.Deployment
{
    /// <summary>
    /// Deploys a contract to a node using HTTP
    /// </summary>
    public class HttpContractDeployer
    {
        private readonly HttpClient client;
        private readonly string deploymentResource;

        public HttpContractDeployer(HttpClient httpClient, string deploymentResource = "")
        {
            this.client = httpClient;
            this.deploymentResource = deploymentResource;
        }

        /// <summary>
        /// Deploys a contract's bytecode to the provided node via HTTP
        /// </summary>
        public async Task<DeploymentResult> DeployAsync(string nodeUrl, BuildCreateContractTransactionRequest model)
        {
            string json = NetJSON.NetJSON.Serialize(model);

            HttpResponseMessage response;

            try
            {
                response = await this.client.PostAsync(
                    GetDeploymentUri(nodeUrl, this.deploymentResource),
                    new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch (HttpRequestException e)
            {
                var errors = new List<string>
                {
                    e.Message,
                };

                if (e.InnerException != null)
                    errors.Add(e.InnerException.Message);

                errors.Add("Please ensure that an instance of your full node is running before trying to deploy a smart contract.");

                return DeploymentResult.DeploymentFailure(errors);
            }

            if (response.IsSuccessStatusCode)
            {
                string successJson = await response.Content.ReadAsStringAsync();
                BuildCreateContractTransactionResponse successResponse =
                    NetJSON.NetJSON.Deserialize<BuildCreateContractTransactionResponse>(successJson);

                if (successResponse.Success)
                    return DeploymentResult.DeploymentSuccess(successResponse);
                else
                    return DeploymentResult.DeploymentFailure(successResponse);
            }

            if (response.Content == null)
            {
                return DeploymentResult.DeploymentFailure(response.StatusCode.ToString());
            }

            string responseBody = await response.Content.ReadAsStringAsync();

            try
            {
                ErrorResponse resp = NetJSON.NetJSON.Deserialize<ErrorResponse>(responseBody);

                return DeploymentResult.DeploymentFailure(resp.Errors.Select(err => err.Message));
            }
            catch (Exception)
            {
                return DeploymentResult.DeploymentFailure(responseBody);
            }
        }

        /// <summary>
        /// Creates a new URI based on a node and a resource
        /// </summary>
        public static Uri GetDeploymentUri(string nodeUrl, string resource)
        {
            return new Uri(new Uri(nodeUrl), resource);
        }
    }
}