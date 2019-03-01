using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.RestClients
{
    /// <summary>Client for making API calls for methods provided by controllers.</summary>
    public abstract class RestApiClientBase
    {
        private readonly IHttpClientFactory httpClientFactory;

        private readonly ILogger logger;

        /// <summary>URL of API endpoint.</summary>
        private readonly string endpointUrl;

        public const int RetryCount = 3;

        /// <summary>Delay between retries.</summary>
        private const int AttemptDelayMs = 1000;

        public const int TimeoutMs = 60_000;

        private readonly RetryPolicy policy;

        public RestApiClientBase(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.endpointUrl = $"http://localhost:{settings.CounterChainApiPort}/api/FederationGateway";

            this.policy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(retryCount: RetryCount, sleepDurationProvider:
                attemptNumber =>
                {
                    // Intervals between new attempts are growing.
                    int delayMs = AttemptDelayMs;

                    if (attemptNumber > 1)
                        delayMs *= attemptNumber;

                    return TimeSpan.FromMilliseconds(delayMs);
                }, onRetry: OnRetry);
        }

        protected async Task<HttpResponseMessage> SendPostRequestAsync<Model>(Model requestModel, string apiMethodName, CancellationToken cancellation) where Model : class
        {
            if (requestModel == null)
                throw new ArgumentException($"{nameof(requestModel)} can't be null.");

            var publicationUri = new Uri($"{this.endpointUrl}/{apiMethodName}");

            HttpResponseMessage response = null;

            using (HttpClient client = this.httpClientFactory.CreateClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);

                var request = new JsonContent(requestModel);

                try
                {
                    // Retry the following call according to the policy.
                    await this.policy.ExecuteAsync(async token =>
                    {
                        this.logger.LogDebug("Sending request of type '{0}' to Uri '{1}'.",
                            requestModel.GetType().FullName, publicationUri);

                        response = await client.PostAsync(publicationUri, request, cancellation).ConfigureAwait(false);
                        this.logger.LogDebug("Response received: {0}", response);

                    }, cancellation);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebug("Operation canceled.");
                    this.logger.LogTrace("(-)[CANCELLED]:null");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    this.logger.LogError("The counter-chain daemon is not ready to receive API calls at this time ({0})", publicationUri);
                    this.logger.LogError("Failed to send a message. Exception: '{0}'.", ex);
                    return new HttpResponseMessage() { ReasonPhrase = ex.Message, StatusCode = HttpStatusCode.InternalServerError };
                }
            }

            this.logger.LogTrace("(-)[SUCCESS]");
            return response;
        }

        protected async Task<Response> SendPostRequestAsync<Model, Response>(Model requestModel, string apiMethodName, CancellationToken cancellation) where Response : class where Model : class
        {
            HttpResponseMessage response = await this.SendPostRequestAsync(requestModel, apiMethodName, cancellation).ConfigureAwait(false);

            // Parse response.
            if ((response != null) && response.IsSuccessStatusCode && (response.Content != null))
            {
                string successJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (successJson != null)
                {
                    Response responseModel = JsonConvert.DeserializeObject<Response>(successJson);

                    this.logger.LogTrace("(-)[SUCCESS]");
                    return responseModel;
                }
            }

            this.logger.LogTrace("(-)[NO_CONTENT]:null");
            return null;
        }

        protected virtual void OnRetry(Exception exception, TimeSpan delay)
        {
            this.logger.LogDebug("Exception while calling API method: {0}.", exception.ToString());
        }
    }
}
