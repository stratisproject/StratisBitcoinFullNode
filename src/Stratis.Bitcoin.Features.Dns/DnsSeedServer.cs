using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DNS.Client;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Protocol.Utils;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
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
        /// Sets the period by which metrics are logged (secs).
        /// </summary>
        private const int MetricsLogRate = 20;

        /// <summary>
        /// Sets the period by which the master file is saved (secs).
        /// </summary>
        private const int SaveMasterfileRate = 300;

        /// <summary>
        /// Defines the output format for a metric row.
        /// </summary>
        private readonly string MetricsOutputFormat = "{0,-60}: {1,20}" + Environment.NewLine;

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
        private readonly IUdpClient udpClient;

        /// <summary>
        /// Defines a factory for creating async loops.
        /// </summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>
        /// Defines a node lifetime object.
        /// </summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Defines the date-time provider.
        /// </summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Defines the configuration settings for the DNS seed server.
        /// </summary>
        private readonly DnsSettings dnsSettings;

        /// <summary>
        /// Defines the data folders of the system.
        /// </summary>
        private readonly DataFolder dataFolders;

        /// <summary>
        /// Defines a metrics async loop.
        /// </summary>
        private IAsyncLoop metricsLoop;

        /// <summary>
        /// Defines a save masterfile loop.
        /// </summary>
        private IAsyncLoop saveMasterfileLoop;

        /// <summary>
        /// Defines an entity that holds the metrics for the DNS server.
        /// </summary>
        private DnsMetric metrics;

        /// <summary>
        /// Defines the pointer to the record number of the results, used to control round-robin so the same peer
        /// doesn't always appear at the top of the list.
        /// </summary>
        private volatile int startIndex = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsSeedServer"/> class with the port to listen on.
        /// </summary>
        /// <param name="client">The UDP client to use to receive DNS requests and send DNS responses.</param>
        /// <param name="masterFile">The initial DNS masterfile.</param>
        /// <param name="asyncLoopFactory">The async loop factory.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="dateTimeProvider">The <see cref="DateTime"/> provider.</param>
        /// <param name="dataFolders">The data folders of the system.</param>
        public DnsSeedServer(IUdpClient client, IMasterFile masterFile, IAsyncLoopFactory asyncLoopFactory, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, DnsSettings dnsSettings, DataFolder dataFolders)
        {
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(masterFile, nameof(masterFile));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(dnsSettings, nameof(dnsSettings));
            Guard.NotNull(dataFolders, nameof(dataFolders));

            this.udpClient = client;
            this.masterFile = masterFile;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.dnsSettings = dnsSettings;
            this.dataFolders = dataFolders;
            this.metrics = new DnsMetric();
        }

        /// <summary>
        /// Gets the current <see cref="IMasterFile"/> instance associated with the <see cref="IDnsServer"/>.
        /// </summary>
        public IMasterFile MasterFile
        {
            get { return this.masterFile; }
        }

        /// <summary>
        /// Gets the metrics for the DNS server.
        /// </summary>
        public DnsMetric Metrics
        {
            get { return this.metrics; }
        }

        /// <summary>
        /// Initializes the DNS Server.
        /// </summary>
        public void Initialize()
        {
            // Load masterfile from disk if it exists.
            lock (this.masterFileLock)
            {
                string path = Path.Combine(this.dataFolders.DnsMasterFilePath, DnsFeature.DnsMasterFileName);
                if (File.Exists(path))
                {
                    this.logger.LogInformation("Loading cached DNS masterfile from {0}", path);

                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        this.MasterFile.Load(stream);
                    }
                }
                else
                {
                    // Seed with SOA and NS resource records when this is a new masterfile.
                    this.SeedMasterFile(this.MasterFile);
                }
            }

            // Create async loop for outputting metrics.
            this.metricsLoop = this.asyncLoopFactory.Run(nameof(this.LogMetrics), async (token) => await Task.Run(() => this.LogMetrics()), this.nodeLifetime.ApplicationStopping, repeatEvery: TimeSpan.FromSeconds(MetricsLogRate));

            // Create async loop for saving the master file.
            this.StartSaveMasterfileLoop();
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
                // Start listening on UDP port.
                this.udpClient.StartListening(dnsListenPort);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Tuple<IPEndPoint, byte[]> request = await this.udpClient.ReceiveAsync();

                        this.logger.LogTrace("DNS request received of size {0} from endpoint {1}.", request.Item2.Length, request.Item1);

                        // Received a request, now handle it. (measured)
                        using (new StopwatchDisposable((elapsed) => { this.metrics.CaptureRequestMetrics(this.GetPeerCount(), elapsed, false); }))
                        {
                            await this.HandleRequestAsync(request);
                        }
                    }
                    catch (ArgumentException e)
                    {
                        this.metrics.CaptureRequestMetrics(this.GetPeerCount(), 0, true);
                        this.logger.LogWarning(e, "Failed to process DNS request.");
                    }
                    catch (SocketException e)
                    {
                        this.metrics.CaptureRequestMetrics(this.GetPeerCount(), 0, true);
                        this.logger.LogError(e, "Socket exception {0} whilst receiving UDP request.", e.ErrorCode);
                    }
                }

                // We've been cancelled.
                this.logger.LogTrace("Cancellation requested, shutting down DNS listener.");
                token.ThrowIfCancellationRequested();
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                this.metrics.CaptureServerFailedMetric();
                throw;
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
        /// <param name="newMasterFile">The new masterfile to swap in.</param>
        public void SwapMasterfile(IMasterFile newMasterFile)
        {
            Guard.NotNull(newMasterFile, nameof(newMasterFile));

            // Seed the new masterfile with SOA and NS resource records.
            this.SeedMasterFile(newMasterFile);

            lock (this.masterFileLock)
            {
                // Perform the swap after seeding to avoid modifying the current masterfile.
                this.masterFile = newMasterFile;
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
                    var disposableClient = this.udpClient as IDisposable;
                    disposableClient?.Dispose();

                    this.metricsLoop?.Dispose();

                    this.saveMasterfileLoop?.Dispose();
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

            IList<IResourceRecord> allAnswers = new List<IResourceRecord>();
            foreach (Question question in request.Questions)
            {
                IList<IResourceRecord> answers = this.masterFile.Get(question);
                if (answers.Count > 0)
                {
                    foreach (IResourceRecord answer in answers)
                    {
                        allAnswers.Add(answer);
                    }
                }

                this.logger.LogTrace("{0} answers to the question: domain = {1}, record type = {2}", answers.Count, question.Name, question.Type);
            }

            // Sort output so same order isn't returned every time.
            if (allAnswers.Count > 0)
            {
                // Capture start index.
                int start = this.startIndex;
                if (start >= allAnswers.Count)
                {
                    // Index is beyond answer count, start at beginning.
                    start = 0;
                    this.startIndex = 0;
                }

                // Copy records from start index up to end of array.
                for (int i = start; i < allAnswers.Count; i++)
                {
                    response.AnswerRecords.Add(allAnswers[i]);
                }

                // Copy records from beginning of array, up to start index (or end of array).
                for (int i = 0; i < start && start < allAnswers.Count; i++)
                {
                    response.AnswerRecords.Add(allAnswers[i]);
                }
            }

            // Set new start index.
            Interlocked.Increment(ref this.startIndex);

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

                // Resolve request against masterfile.
                request = new Request(header, Question.GetAllFromArray(udpRequest.Item2, header.Size, header.QuestionCount));
                IResponse response = this.Resolve(request);

                // Send response.
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

                // Try and send response one more time.
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

        /// <summary>
        /// Seeds the given masterfile with the SOA and NS DNS records with the DNS specific settings.
        /// </summary>
        /// <param name="masterFile"></param>
        private void SeedMasterFile(IMasterFile masterFile)
        {
            this.logger.LogInformation("Seeding DNS masterfile with SOA and NS resource records: Host = {0}, Nameserver = {1}, Mailbox = {2}", this.dnsSettings.DnsHostName, this.dnsSettings.DnsNameServer, this.dnsSettings.DnsMailBox);

            masterFile.Seed(this.dnsSettings);
        }

        /// <summary>
        /// Gets the peer count of IP v4 and v6 addresses in the DNS masterfile.
        /// </summary>
        /// <returns></returns>
        private int GetPeerCount()
        {
            int count = this.MasterFile.Get(new Question(new Domain(this.dnsSettings.DnsHostName), RecordType.A)).Count;
            count += this.MasterFile.Get(new Question(new Domain(this.dnsSettings.DnsHostName), RecordType.AAAA)).Count;
            return count;
        }

        /// <summary>
        /// Logs metrics periodically to the console.
        /// </summary>
        private void LogMetrics()
        {
            try
            {
                // Print out total and period values.
                var metricOutput = new StringBuilder();
                metricOutput.AppendLine("==========DNS Metrics==========");
                metricOutput.AppendLine();
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Snapshot Time", this.dateTimeProvider.GetAdjustedTime());
                metricOutput.AppendLine();

                // Metrics since start.
                metricOutput.AppendLine(">>> Metrics since start");
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Total DNS Requests", this.metrics.DnsRequestCountSinceStart);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Total DNS Server Failures (Restarted)", this.metrics.DnsServerFailureCountSinceStart);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Total DNS Request Failures", this.metrics.DnsRequestFailureCountSinceStart);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Maximum Peer Count", this.metrics.MaxPeerCountSinceStart);
                metricOutput.AppendLine();

                // Reset period values.
                DnsMetricSnapshot snapshot = this.metrics.ResetSnapshot();

                // Calculate averages.
                double averagePeerCount = snapshot.DnsRequestCountSinceLastPeriod == 0 ? 0 : snapshot.PeerCountSinceLastPeriod / snapshot.DnsRequestCountSinceLastPeriod;
                double averageElapsedTicks = snapshot.DnsRequestCountSinceLastPeriod == 0 ? 0 : snapshot.DnsRequestElapsedTicksSinceLastPeriod / snapshot.DnsRequestCountSinceLastPeriod;

                // Metrics since last period.
                metricOutput.AppendLine($">>> Metrics for last period ({MetricsLogRate} secs)");
                metricOutput.AppendFormat(this.MetricsOutputFormat, "DNS Requests", snapshot.DnsRequestCountSinceLastPeriod);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "DNS Server Failures (Restarted)", snapshot.DnsServerFailureCountSinceLastPeriod);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "DNS Request Failures", snapshot.DnsRequestFailureCountSinceLastPeriod);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Average Peer Count", averagePeerCount);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Last Peer Count", snapshot.LastPeerCount);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Average Elapsed Time Processing DNS Requests (ms)", new TimeSpan((long)averageElapsedTicks).TotalMilliseconds);
                metricOutput.AppendFormat(this.MetricsOutputFormat, "Last Elapsed Time Processing DNS Requests (ms)", new TimeSpan(snapshot.LastDnsRequestElapsedTicks).TotalMilliseconds);
                metricOutput.AppendLine();

                // Output.
                this.logger.LogInformation(metricOutput.ToString());
            }
            catch (Exception e)
            {
                // If metrics fail, just log.
                this.logger.LogWarning(e, "Failed to output DNS metrics.");
            }
        }

        /// <summary>
        /// Starts the loop to save the masterfile.
        /// </summary>
        private void StartSaveMasterfileLoop()
        {
            this.saveMasterfileLoop = this.asyncLoopFactory.Run($"{nameof(DnsFeature)}.WhitelistRefreshLoop", token =>
            {
                string path = Path.Combine(this.dataFolders.DnsMasterFilePath, DnsFeature.DnsMasterFileName);

                this.logger.LogInformation("Saving cached DNS masterfile to {0}", path);

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    this.MasterFile.Save(stream);
                }
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromSeconds(SaveMasterfileRate));
        }
    }
}
