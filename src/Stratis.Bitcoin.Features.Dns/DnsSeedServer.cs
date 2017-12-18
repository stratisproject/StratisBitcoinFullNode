﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DNS.Client;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Protocol.Utils;
using DNS.Server;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// This class defines a DNS server based on 3rd party library https://github.com/kapetan/dns.
    /// </summary>
    public class DnsSeedServer : IDnsServer
    {
        /// <summary>
        /// Sets the timeout at 2 seconds.
        /// </summary>
        private const int UdpTimeout = 2000;

        /// <summary>
        /// Defines a flag used to indicate whether the object has been disposed or not.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Defines the DNS masterfile used to cache IP addresses.
        /// </summary>
        private IMasterFile masterFile;

        /// <summary>
        /// Defines a lock object for the masterfile to use during swapping.
        /// </summary>
        private object masterFileLock = new object();

        /// <summary>
        /// Defines the client used to listen for incoming DNS requests.
        /// </summary>
        private IUdpClient udpClient;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsSeedServer"/> class with the port to listen on.
        /// </summary>
        /// <param name="client">The UDP client to use to receive DNS requests and send DNS responses.</param>
        /// <param name="masterFile">The initial DNS masterfile.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public DnsSeedServer(IUdpClient client, IMasterFile masterFile, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(masterFile, nameof(masterFile));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.udpClient = client;
            this.masterFile = masterFile;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Gets the current <see cref="IMasterFile"/> instance associated with the <see cref="IDnsServer"/>.
        /// </summary>
        public IMasterFile MasterFile
        {
            get { return this.masterFile; }
        }

        /// <summary>
        /// Starts listening for DNS requests.
        /// </summary>
        /// <param name="dnsListenPort">The port to listen on.</param>
        /// <param name="token">The token used to cancel the listen.</param>
        /// <returns>A task used to await the listen operation.</returns>
        public async Task ListenAsync(int dnsListenPort, CancellationToken token)
        {
            try
            {
                this.udpClient.StartListening(dnsListenPort);
            }
            catch (SocketException e)
            {
                this.logger.LogError(e, "Socket exception {0} whilst creating UDP client for DNS service.", e.ErrorCode);
                throw;
            }

            try
            {
                while (true)
                {
                    Tuple<IPEndPoint, byte[]> request;

                    // Have we been cancelled?
                    if (token.IsCancellationRequested)
                    {
                        this.logger.LogTrace("Cancellation requested, shutting down DNS listener.");
                        token.ThrowIfCancellationRequested();
                    }

                    try
                    {
                        request = await this.udpClient.ReceiveAsync();

                        this.logger.LogTrace("DNS request received of size {0} from endpoint {1}.", request.Item2.Length, request.Item1);

                        // Received a request, now handle it
                        await this.HandleRequestAsync(request);
                    }
                    catch (ArgumentException e)
                    {
                        this.logger.LogWarning(e, "Failed to process DNS request.");
                    }
                    catch (SocketException e)
                    {
                        this.logger.LogError(e, "Socket exception {0} whilst receiving UDP request.", e.ErrorCode);
                    }
                }
            }
            finally
            {
                this.udpClient.StopListening();
            }
        }

        /// <summary>
        /// Swaps in a new version of the cached DNS masterfile used by the DNS server.
        /// </summary>
        /// <remarks>
        /// The <see cref="DnsFeature"/> object is designed to produce a whitelist of peers from the <see cref="P2P.IPeerAddressManager"/>
        /// object which is then periodically formed into a new masterfile instance and applied to the <see cref="IDnsServer"/> object.  The
        /// masterfile is swapped for efficiency, rather than applying a merge operation to the existing masterfile, or clearing the existing
        /// masterfile and re-adding the peer entries (which could cause some interim DNS resolve requests to fail).
        /// </remarks>
        /// <param name="masterFile">The new masterfile to swap in.</param>
        public void SwapMasterfile(IMasterFile masterFile)
        {
            Guard.NotNull(masterFile, nameof(masterFile));

            lock (this.masterFileLock)
            {
                this.masterFile = masterFile;
            }
        }

        /// <summary>
        /// Disposes of the object.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the object.
        /// </summary>
        /// <param name="disposing"><c>true</c> if the object is being disposed of deterministically, otherwise <c>false</c>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    IDisposable disposableClient = this.udpClient as IDisposable;
                    disposableClient?.Dispose();
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Resolves the DNS request against the local masterfile.
        /// </summary>
        /// <param name="request">The request to resolve against the local masterfile.</param>
        /// <returns>A DNS response.</returns>
        private IResponse Resolve(Request request)
        {
            Response response = Response.FromRequest(request);

            foreach (Question question in request.Questions)
            {
                IList<IResourceRecord> answers = this.masterFile.Get(question);
                if (answers.Count > 0)
                {
                    foreach (IResourceRecord answer in answers)
                    {
                        response.AnswerRecords.Add(answer);
                    }
                }

                this.logger.LogTrace("{0} answers to the question: domain = {1}, record type = {2}", answers.Count, question.Name, question.Type);
            }

            return response;
        }

        /// <summary>
        /// Handles a DNS request received by the UDP client.
        /// </summary>
        /// <param name="udpRequest">The DNS request received from the UDP client.</param>
        private async Task HandleRequestAsync(Tuple<IPEndPoint, byte[]> udpRequest)
        {
            Request request = null;

            try
            {
                // Get request from received message (would use 3rd party library Request.FromArray but it doesn't handle additional record
                // flag properly, which dig requests).
                Header header = Header.FromArray(udpRequest.Item2);
                if (header.Response)
                {
                    throw new ArgumentException("Request message is actually flagged as a response.");
                }

                if (header.QuestionCount == 0)
                {
                    throw new ArgumentException("Request message contains no questions.");
                }

                if (header.ResponseCode != ResponseCode.NoError)
                {
                    throw new ArgumentException($"Request message contains a non-error response code of {header.ResponseCode}.");
                }

                // Resolve request against masterfile
                request = new Request(header, Question.GetAllFromArray(udpRequest.Item2, header.Size, header.QuestionCount));
                IResponse response = this.Resolve(request);

                // Send response
                await this.udpClient.SendAsync(response.ToArray(), response.Size, udpRequest.Item1).WithCancellationTimeout(UdpTimeout);
            }
            catch (SocketException e)
            {
                this.logger.LogError(e, "Socket error {0} whilst sending DNS response to {1}", e.ErrorCode, udpRequest.Item1);
            }
            catch (OperationCanceledException e)
            {
                this.logger.LogError(e, "Sending DNS response to {0} timed out.", udpRequest.Item1);
            }
            catch (ResponseException e)
            {
                this.logger.LogError(e, "Received error {0} when sending DNS response to {1}, trying again.", e.Response.ResponseCode, udpRequest.Item1);

                // Try and send response one more time
                IResponse response = e.Response;
                if (response == null)
                {
                    response = Response.FromRequest(request);
                }

                try
                {
                    await this.udpClient.SendAsync(response.ToArray(), response.Size, udpRequest.Item1).WithCancellationTimeout(UdpTimeout);
                }
                catch (SocketException ex)
                {
                    this.logger.LogError(ex, "Socket error {0} whilst sending DNS response to {1}", ex.ErrorCode, udpRequest.Item1);
                }
                catch (OperationCanceledException ex)
                {
                    this.logger.LogError(ex, "Sending DNS response to {0} timed out.", udpRequest.Item1);
                }
            }
        }
    }
}
