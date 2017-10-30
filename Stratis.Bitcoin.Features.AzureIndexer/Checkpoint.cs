using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class Checkpoint
    {
        private readonly string _CheckpointName;
        public string CheckpointName
        {
            get
            {
                return _CheckpointName;
            }
        }

        CloudBlockBlob _Blob;
        public Checkpoint(string checkpointName, Network network, Stream data, CloudBlockBlob blob)
        {
            if (checkpointName == null)
                throw new ArgumentNullException("checkpointName");
            _Blob = blob;
            _CheckpointName = checkpointName;
            _BlockLocator = new BlockLocator();
            if (data != null)
            {
                try
                {
                    _BlockLocator.ReadWrite(data, false);
                    return;
                }
                catch
                {
                }
            }
            var list = new List<uint256>();
            list.Add(network.GetGenesis().Header.GetHash());
            _BlockLocator = new BlockLocator();
            _BlockLocator.Blocks.AddRange(list);
        }

        public uint256 Genesis
        {
            get
            {
                return BlockLocator.Blocks[BlockLocator.Blocks.Count - 1];
            }
        }

        BlockLocator _BlockLocator;
        public BlockLocator BlockLocator
        {
            get
            {
                return _BlockLocator;
            }
        }

        public bool SaveProgress(ChainedBlock tip)
        {
            return SaveProgress(tip.GetLocator());
        }
        public bool SaveProgress(BlockLocator locator)
        {
            _BlockLocator = locator;
            try
            {
                return SaveProgressAsync().Result;
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                return false;
            }
        }

        public async Task DeleteAsync()
        {
            try
            {
                await _Blob.DeleteAsync().ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 404)
                    return;
                throw;
            }
        }

        private async Task<bool> SaveProgressAsync()
        {
            var bytes = BlockLocator.ToBytes();
            try
            {

                await _Blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length, new AccessCondition()
                {
                    IfMatchETag = _Blob.Properties.ETag
                }, null, null).ConfigureAwait(false);
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation != null && ex.RequestInformation.HttpStatusCode == 412)
                    return false;
                throw;
            }
            return true;
        }

        public static async Task<Checkpoint> LoadBlobAsync(CloudBlockBlob blob, Network network)
        {
            var checkpointName = string.Join("/", blob.Name.Split('/').Skip(1).ToArray());
            MemoryStream ms = new MemoryStream();
            try
            {
                await blob.DownloadToStreamAsync(ms).ConfigureAwait(false);
                ms.Position = 0;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null || ex.RequestInformation.HttpStatusCode != 404)
                    throw;
            }
            var checkpoint = new Checkpoint(checkpointName, network, ms, blob);
            return checkpoint;
        }

        public override string ToString()
        {
            return CheckpointName;
        }
    }

}
