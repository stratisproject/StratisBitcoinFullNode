using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Single historic sample item for quality score calculations.
    /// </summary>
    public class PeerSample
    {
        /// <summary>Peer who provided the sample.</summary>
        public IBlockPullerBehavior peer { get; set; }

        /// <summary>Downloading speed as number of milliseconds per KB.</summary>
        public double timePerKb { get; set; }
    }

    /// <summary>
    /// Implements logic of evaluation of quality of node network peers based on 
    /// the recent past experience with them with respect to other node's network peers.
    /// </summary>
    /// <remarks>
    /// Each peer is assigned with a quality score, which is a floating point number between 
    /// <see cref="MinScore"/> and <see cref="MaxScore"/> inclusive. Each peer starts with 
    /// the score in the middle of the score interval. The higher the score, the better the peer 
    /// and the better chance for the peer to get more work assigned.
    /// </remarks>
    public class QualityScore
    {
        /// <summary>Maximal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const double MinScore = 1.0;
        /// <summary>Minimal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const double MaxScore = 150.0;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Lock object to protect access to samples statistics.</summary>
        private readonly object lockObject = new object();

        /// <summary>Average time of a block download among up to last <see cref="samplesCount"/> blocks.</summary>
        /// <remarks>
        /// Write access to this object has to be protected by <see cref="lockObject"/>.
        /// <para>
        /// Public getter allows better testing of the class.
        /// </para>
        /// </remarks>
        public double AverageBlockTimePerKb { get; private set; }

        /// <summary>Number of block time samples available in <see cref="samples"/>.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private int samplesCount { get; set; }

        /// <summary>Index in <see cref="samples"/> array where the next sample will be stored.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private int samplesIndex { get; set; }

        /// <summary>Circular array of recent block times in milliseconds per KB, which contains <see cref="samplesCount"/> samples.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly PeerSample[] samples;

        /// <summary>Reference counter for peers. This is used for calculating how many peers contributed to the sample history we keep.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<IBlockPullerBehavior, int> peerReferenceCounter;

        /// <summary>Sum of all samples in <see cref="samples"/> array.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private double samplesSum { get; set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="maxSampleCount">Maximal number of samples we calculate statistics from.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public QualityScore(int maxSampleCount, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.samples = new PeerSample[maxSampleCount];
            for (int i = 0; i < this.samples.Length; i++)
                this.samples[i] = new PeerSample();

            this.AverageBlockTimePerKb = 0.0;
            this.peerReferenceCounter = new Dictionary<IBlockPullerBehavior, int>();
        }

        /// <summary>
        /// Adds new time of a block to the list of times of recently downloaded blocks.
        /// </summary>
        /// <param name="peer">Peer that downloaded the block.</param>
        /// <param name="blockDownloadTimeMs">Time in milliseconds it took to download the block from the peer.</param>
        /// <param name="blockSize">Size of the downloaded block in bytes.</param>
        public void AddSample(IBlockPullerBehavior peer, long blockDownloadTimeMs, int blockSize)
        {
            this.logger.LogTrace($"({nameof(peer)}:{peer.GetHashCode():x},{nameof(blockDownloadTimeMs)}:{blockDownloadTimeMs},{nameof(blockSize)}:{blockSize})");

            double timePerKb = 1024.0 * (double)blockDownloadTimeMs / (double)blockSize;
            if (timePerKb < 0.00001) timePerKb = 0.00001;

            lock (this.lockObject)
            {
                // If we reached the maximum number of samples, we need to remove oldest sample.
                if (this.samplesCount == this.samples.Length)
                {
                    PeerSample oldSample = this.samples[this.samplesIndex];
                    this.samplesSum -= oldSample.timePerKb;
                    this.peerReferenceCounter[oldSample.peer]--;

                    if (this.peerReferenceCounter[oldSample.peer] == 0)
                        this.peerReferenceCounter.Remove(oldSample.peer);
                }
                else this.samplesCount++;

                // Add new sample to the mix.
                this.samples[this.samplesIndex].timePerKb = timePerKb;
                this.samples[this.samplesIndex].peer = peer;
                this.samplesIndex = (this.samplesIndex + 1) % this.samples.Length;

                if (this.peerReferenceCounter.ContainsKey(peer)) this.peerReferenceCounter[peer]++;
                else this.peerReferenceCounter.Add(peer, 1);

                // Update the sum and the average with the latest data.
                this.samplesSum += timePerKb;
                this.AverageBlockTimePerKb = this.samplesSum / this.samplesCount;
            }

            this.logger.LogTrace($"(-):{nameof(this.AverageBlockTimePerKb)}={this.AverageBlockTimePerKb}");
        }

        /// <summary>
        /// Calculates adjustment of peer's quality score when it finished downloading a block.
        /// </summary>
        /// <param name="blockDownloadTimeMs">Time in milliseconds it took to download the block from the peer.</param>
        /// <param name="blockSize">Size of the downloaded block in bytes.</param>
        /// <returns>Quality score adjustment for the peer.</returns>
        public double CalculateQualityAdjustment(long blockDownloadTimeMs, int blockSize)
        {
            this.logger.LogTrace($"({nameof(blockDownloadTimeMs)}:{blockDownloadTimeMs},{nameof(blockSize)}:{blockSize})");

            double avgTimePerKb = this.AverageBlockTimePerKb;
            double timePerKb = 1024.0 * (double)blockDownloadTimeMs / (double)blockSize;
            if (timePerKb < 0.00001) timePerKb = 0.00001;

            this.logger.LogTrace($"Average time per KB is {avgTimePerKb} ms, this sample is {timePerKb} ms/KB.");

            // If the block was received with better speed than is 2x average of the recent history we keep
            // then we reward the peer for downloading it quickly. Otherwise, we penalize the peer for downloading 
            // the block too slowly. If we have no history, then we give a small reward no matter what.
            double res = 0.1;
            if (timePerKb < 2 * avgTimePerKb) res = avgTimePerKb / timePerKb;
            else if (Math.Abs(avgTimePerKb) >= 0.00001) res = -timePerKb / (2 * avgTimePerKb);

            if ((res < 0) && this.IsPenaltyDiscarded())
                res = 0;

            this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Calculates peer's penalty when the wait for the next block times out.
        /// </summary>
        /// <returns>Quality score penalty for the peer.</returns>
        public double CalculateNextBlockTimeoutQualityPenalty()
        {
            this.logger.LogTrace("()");

            double res = this.IsPenaltyDiscarded() ? 0 : -1;

            this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Checks whether penalty should be avoided. This is when sum(score) of all peers is lower than 2x number of peers peerCount.
        /// This mechanism also prevents single peer to go to minimum if it is alone.
        /// </summary>
        /// <returns><c>true</c> if the penalty should be discarded, <c>false</c> otherwise.</returns>
        public bool IsPenaltyDiscarded()
        {
            this.logger.LogTrace("()");

            int peerCount = 0;
            double peerQualitySum = 0;
            lock (this.lockObject)
            {
                peerCount = this.peerReferenceCounter.Keys.Count;
                peerQualitySum = this.peerReferenceCounter.Keys.Sum(p => p.QualityScore);
            }
            this.logger.LogTrace($"Number of peers is {peerCount}, sum of peer qualities is {peerQualitySum}.");
            bool res = peerQualitySum < 2 * peerCount;

            this.logger.LogTrace($"(-):{res}");
            return res;
        }
    }
}
