using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer.IndexTasks
{
    public class IndexTableEntitiesTask : IndexTableEntitiesTaskBase<ITableEntity>
    {
        CloudTable _Table;
        public IndexTableEntitiesTask(IndexerConfiguration conf, CloudTable table)
            : base(conf)
        {
            _Table = table;
        }

        protected override CloudTable GetCloudTable()
        {
            return _Table;
        }

        protected override ITableEntity ToTableEntity(ITableEntity item)
        {
            return item;
        }

        protected override void ProcessBlock(BlockInfo block, BulkImport<ITableEntity> bulk)
        {
            throw new NotSupportedException();
        }


        public void Index(IEnumerable<ITableEntity> entities, TaskScheduler taskScheduler)
        {
            try
            {
                IndexAsync(entities, taskScheduler).Wait();
            }
            catch (AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                throw;
            }
        }
        public Task IndexAsync(IEnumerable<ITableEntity> entities, TaskScheduler taskScheduler)
        {
            taskScheduler = taskScheduler ?? TaskScheduler.Default;
            var tasks = entities
                .GroupBy(e => e.PartitionKey)
                .SelectMany(group => group
                                    .Partition(PartitionSize)
                                    .Select(batch => new Task(() => IndexCore(group.Key, batch))))
                .ToArray();
            foreach (var t in tasks)
            {
                t.Start(taskScheduler);
            }
            return Task.WhenAll(tasks);
        }
    }
    public abstract class IndexTableEntitiesTaskBase<TIndexed> : IndexTask<TIndexed>
    {
        public IndexTableEntitiesTaskBase(IndexerConfiguration configuration)
            : base(configuration)
        {

        }

        int _IndexedEntities = 0;
        public int IndexedEntities
        {
            get
            {
                return _IndexedEntities;
            }
        }
        protected override int PartitionSize
        {
            get
            {
                return 100;
            }
        }


        protected override Task EnsureSetup()
        {
            return GetCloudTable().CreateIfNotExistsAsync();
        }

        protected abstract CloudTable GetCloudTable();
        protected abstract ITableEntity ToTableEntity(TIndexed item);


        protected override void IndexCore(string partitionName, IEnumerable<TIndexed> items)
        {
            var batch = new TableBatchOperation();
            foreach (var item in items)
            {
                batch.Add(TableOperation.InsertOrReplace(ToTableEntity(item)));
            }

            var table = GetCloudTable();

            var options = new TableRequestOptions()
            {
                PayloadFormat = TablePayloadFormat.Json,
                MaximumExecutionTime = _Timeout,
                ServerTimeout = _Timeout,
            };

            var context = new OperationContext();
            Queue<TableBatchOperation> batches = new Queue<TableBatchOperation>();
            batches.Enqueue(batch);

            while (batches.Count > 0)
            {
                batch = batches.Dequeue();
                try
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    if (batch.Count > 1)
                        table.ExecuteBatchAsync(batch, options, context).GetAwaiter().GetResult();
                    else
                    {
                        if (batch.Count == 1)
                            table.ExecuteAsync(batch[0], options, context).GetAwaiter().GetResult();
                    }
                    Interlocked.Add(ref _IndexedEntities, batch.Count);
                }
                catch (Exception ex)
                {
                    if (IsError413(ex))
                    {
                        var split = batch.Count / 2;
                        var batch1 = batch.Take(split).ToList();
                        var batch2 = batch.Skip(split).Take(batch.Count - split).ToList();
                        batches.Enqueue(ToBatch(batch1));
                        batches.Enqueue(ToBatch(batch2));
                    }
                    else if (Helper.IsError(ex, "EntityTooLarge"))
                    {
                        var op = GetFaultyOperation(ex, batch);
                        var entity = (DynamicTableEntity)GetEntity(op);
                        var serialized = entity.Serialize();

                        Configuration
                            .GetBlocksContainer()
                            .GetBlockBlobReference(entity.GetFatBlobName())
                            .UploadFromByteArrayAsync(serialized, 0, serialized.Length).GetAwaiter().GetResult();
                        entity.MakeFat(serialized.Length);                       
                        batches.Enqueue(batch);
                    }
                    else
                    {
                        IndexerTrace.ErrorWhileImportingEntitiesToAzure(batch.Select(b => GetEntity(b)).ToArray(), ex);
                        batches.Enqueue(batch);
                        throw;
                    }
                }
            }
        }


        private ITableEntity GetEntity(TableOperation op)
        {
            return (ITableEntity)typeof(TableOperation).GetProperty("Entity", BindingFlags.Instance |
                            BindingFlags.NonPublic |
                            BindingFlags.Public)
                            .GetValue(op);
        }


        private bool IsError413(Exception ex)
        {
            var storage = ex as StorageException;
            if (storage == null)
                return false;
            return storage.RequestInformation != null && storage.RequestInformation.HttpStatusCode == 413;
        }

        private TableOperation GetFaultyOperation(Exception ex, TableBatchOperation batch)
        {
            if(batch.Count == 1)
                return batch[0];
            var storage = ex as StorageException;
            if (storage == null)
                return null;
            if (storage.RequestInformation != null
               && storage.RequestInformation.ExtendedErrorInformation != null)
            {
                var match = Regex.Match(storage.RequestInformation.ExtendedErrorInformation.ErrorMessage, "[0-9]*");
                if (match.Success)
                    return batch[int.Parse(match.Value)];
            }
            return null;
        }
        private TableBatchOperation ToBatch(List<TableOperation> ops)
        {
            var op = new TableBatchOperation();
            foreach(var operation in ops)
                op.Add(operation);
            return op;
        }        


    }
}
