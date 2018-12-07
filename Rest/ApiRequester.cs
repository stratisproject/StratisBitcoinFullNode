using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Settings;
using RestSharp;
using Newtonsoft.Json;
using System.Net;

namespace Stratis.FederatedSidechains.AdminDashboard.Rest
{
    public static class ApiRequester
    {
        /// <summary>
        /// Make a HTTP request to a specified node
        /// </summary>
        /// <param name="endpoint">HTTP node endpoint</param>
        /// <param name="path">URL</param>
        /// <returns>An ApiResponse object</returns>
        public static async Task<ApiResponse> GetRequestAsync(string endpoint, string path)
        {
            var restClient = new RestClient(string.Concat(endpoint, path));
            var restRequest = new RestRequest(Method.GET);
            IRestResponse restResponse = await restClient.ExecuteTaskAsync(restRequest);
            return new ApiResponse
            {
                IsSuccess = restResponse.StatusCode.Equals(HttpStatusCode.OK),
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
        public static async Task<ApiResponse> PostRequestAsync(string endpoint, string path, object body)
        {
            var restClient = new RestClient(string.Concat(endpoint, path));
            var restRequest = new RestRequest(Method.POST);
            restRequest.AddHeader("Content-type", "application/json");
            restRequest.AddJsonBody(body);
            IRestResponse restResponse = await restClient.ExecuteTaskAsync(restRequest);
            return new ApiResponse
            {
                IsSuccess = restResponse.StatusCode.Equals(HttpStatusCode.OK),
                Content = JsonConvert.DeserializeObject(restResponse.Content)
            };
        }
    }
}