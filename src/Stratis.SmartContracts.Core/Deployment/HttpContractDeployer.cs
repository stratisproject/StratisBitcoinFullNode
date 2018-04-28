using System;
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
        /// <param name="node"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<DeploymentResult> DeployAsync(
            string node,
            BuildCreateContractTransactionRequest model)
        {
            string json = NetJSON.NetJSON.Serialize(model);

            HttpResponseMessage response;

            try
            {
                response = await this.client.PostAsync(
                    GetDeploymentUri(node, this.deploymentResource),
                    new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch (HttpRequestException e)
            {
                return DeploymentResult.DeploymentFailure(e.Message);
            }

            if (response.IsSuccessStatusCode)
            {
                string successJson = await response.Content.ReadAsStringAsync();
                var successResponse =
                    NetJSON.NetJSON.Deserialize<BuildCreateContractTransactionResponse>(successJson);

                return DeploymentResult.DeploymentSuccess(successResponse.NewContractAddress);
            }

            if (response.Content == null)
            {
                return DeploymentResult.DeploymentFailure(response.StatusCode.ToString());
            }

            string responseBody = await response.Content.ReadAsStringAsync();

            try
            {
                var resp = NetJSON.NetJSON.Deserialize<ErrorResponse>(responseBody);

                return DeploymentResult.DeploymentFailure(resp.Errors.Select(err => err.Message));
            }
            catch (Exception e)
            {
                return DeploymentResult.DeploymentFailure(responseBody);
            }
        }

        /// <summary>
        /// Creates a new URI based on a node and a resource
        /// </summary>
        /// <param name="node"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public static Uri GetDeploymentUri(string node, string resource)
        {
            return new Uri(new Uri(node), resource);
        }
    }
}
