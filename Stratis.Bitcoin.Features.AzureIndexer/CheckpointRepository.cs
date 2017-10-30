using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class CheckpointRepository
    {
        public CheckpointRepository(CloudBlobContainer container, Network network, string checkpointSet)
        {
            _Network = network;
            _Container = container;
            CheckpointSet = checkpointSet;
        }

        public string CheckpointSet
        {
            get;
            set;
        }
        CloudBlobContainer _Container;
        Network _Network;
        public Task<Checkpoint> GetCheckpointAsync(string checkpointName)
        {
            var blob = _Container.GetBlockBlobReference("Checkpoints/" + GetSetPart(checkpointName));
            return Checkpoint.LoadBlobAsync(blob, _Network);
        }

        private string GetSetPart(string checkpointName)
        {
            bool isLocal = !checkpointName.Contains('/');
            if (isLocal)
                return GetSetPart() + checkpointName;
            if (checkpointName.StartsWith("/"))
                checkpointName = checkpointName.Substring(1);
            return checkpointName;
        }

        private string GetSetPart()
        {
            if (CheckpointSet == null)
                return "";
            return CheckpointSet + "/";
        }

        public Task<Checkpoint[]> GetCheckpointsAsync()
        {
            List<Task<Checkpoint>> checkpoints = new List<Task<Checkpoint>>();

            foreach (var blob in _Container.ListBlobsAsync("Checkpoints/" + GetSetPart(), true, BlobListingDetails.None).GetAwaiter().GetResult().OfType<CloudBlockBlob>())
            {
                checkpoints.Add(Checkpoint.LoadBlobAsync(blob, _Network));
            }
            return Task.WhenAll(checkpoints.ToArray());
        }

        public async Task DeleteCheckpointsAsync()
        {
            List<Task> deletions = new List<Task>();
            var checkpoints = await GetCheckpointsAsync().ConfigureAwait(false);
            foreach (var checkpoint in checkpoints)
            {
                deletions.Add(checkpoint.DeleteAsync());
            }
            await Task.WhenAll(deletions.ToArray()).ConfigureAwait(false);
        }

        public void DeleteCheckpoints()
        {
            try
            {
                DeleteCheckpointsAsync().Wait();
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
        }

        public Checkpoint GetCheckpoint(string checkpointName)
        {
            try
            {
                return GetCheckpointAsync(checkpointName).Result;
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                return null;
            }
        }
    }

    public static class CloudBlobContainerExtensions
    {
        public static async Task<IList<IListBlobItem>> ListBlobsAsync(this CloudBlobContainer blobContainer, string prefix, bool useFlatBlobListing, BlobListingDetails blobListingDetails, CancellationToken ct = default(CancellationToken), Action<IList<IListBlobItem>> onProgress = null)
        {
            var items = new List<IListBlobItem>();
            BlobContinuationToken token = null;

            do
            {
                var seg = await blobContainer.ListBlobsSegmentedAsync(prefix, useFlatBlobListing, blobListingDetails, null, token, null, null);
                token = seg.ContinuationToken;
                items.AddRange(seg.Results);
                if (onProgress != null) onProgress(items);

            } while (token != null && !ct.IsCancellationRequested);

            return items;
        }
    }
}
