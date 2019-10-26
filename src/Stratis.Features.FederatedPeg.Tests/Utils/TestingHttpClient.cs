using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Stratis.Features.FederatedPeg.Tests.Utils
{
    public static class TestingHttpClient
    {
        public static void PrepareWorkingHttpClient(ref HttpMessageHandler httpMessageHandler, ref HttpClient httpClient, ref IHttpClientFactory httpClientFactory)
        {
            PrepareHttpClient(ref httpMessageHandler, ref httpClient, ref httpClientFactory);
        }

        public static void PrepareFailingHttpClient(ref HttpMessageHandler httpMessageHandler, ref HttpClient httpClient, ref IHttpClientFactory httpClientFactory)
        {
            PrepareHttpClient(ref httpMessageHandler, ref httpClient, ref httpClientFactory, true);
        }

        private static void PrepareHttpClient(ref HttpMessageHandler httpMessageHandler, ref HttpClient httpClient, ref IHttpClientFactory httpClientFactory, bool failingClient = false)
        {
            httpMessageHandler = Substitute.ForPartsOf<HttpMessageHandler>();

            object sendCall = httpMessageHandler.Protected("SendAsync", Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());

            if (failingClient)
                sendCall.ThrowsForAnyArgs(new HttpRequestException("failed"));
            else
                sendCall.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

            httpClient = new HttpClient(httpMessageHandler);
            httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        }
    }
}
