using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using Stratis.FederatedSidechains.AdminDashboard.Entities;
using Stratis.FederatedSidechains.AdminDashboard.Helpers;

namespace Stratis.FederatedSidechains.AdminDashboard.Services
{
    public class ApiRequester
    {
        private readonly ILogger<ApiRequester> logger;

        public ApiRequester(ILogger<ApiRequester> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Make a HTTP request to a specified node
        /// </summary>
        /// <param name="endpoint">HTTP node endpoint</param>
        /// <param name="path">URL</param>
        /// <returns>An ApiResponse object</returns>
        public async Task<ApiResponse> GetRequestAsync(string endpoint, string path, string query = null)
        {
            var restClient = new RestClient(UriHelper.BuildUri(endpoint, path, query));
            var restRequest = new RestRequest(Method.GET);
            IRestResponse restResponse = await restClient.ExecuteTaskAsync(restRequest);
            var isSuccess = restResponse.StatusCode.Equals(HttpStatusCode.OK);
            return new ApiResponse
            {
                IsSuccess = isSuccess,
                Content = JsonConvert.DeserializeObject(restResponse.Content)
            };
        }

        /// <summary>
        /// Make a HTTP request with POST method
        /// </summary>
        /// <param name="endpoint">HTTP node endpoint</param>
        /// <param name="path">URL</param>
        /// <param name="body">Specify the body request</param>
        /// <returns>An ApiResponse object</returns>
        public async Task<ApiResponse> PostRequestAsync(string endpoint, string path, object body, Method method = Method.POST)
        {
            var restClient = new RestClient(UriHelper.BuildUri(endpoint, path));
            var restRequest = new RestRequest(method);
            restRequest.AddHeader("Content-type", "application/json");
            restRequest.AddJsonBody(body);
            IRestResponse restResponse = await restClient.ExecuteTaskAsync(restRequest);
            var isSuccess = restResponse.StatusCode.Equals(HttpStatusCode.OK);
            return new ApiResponse
            {
                IsSuccess = isSuccess,
                Content = JsonConvert.DeserializeObject(restResponse.Content)
            };
        }
    }
}