using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer.IndexTasks
{
    public class BulkImport<T>
    {
        public BulkImport(int partitionSize)
        {
            this.PartitionSize = partitionSize;
        }
        public int PartitionSize
        {
            get;
            set;
        }
        Dictionary<string, Queue<T>> _CurrentPartitions = new Dictionary<string, Queue<T>>();
        public void Add(string partitionName, T item)
        {
            var partition = GetPartition(partitionName);
            partition.Enqueue(item);
            if (partition.Count >= PartitionSize)
            {
                T[] fullPartition = new T[PartitionSize];
                for (int i = 0 ; i < PartitionSize ; i++)
                {
                    fullPartition[i] = partition.Dequeue();
                }
                _ReadyPartitions.Enqueue(Tuple.Create(partitionName, fullPartition));
            }
        }

        public void FlushUncompletePartitions()
        {
            foreach (var partition in _CurrentPartitions)
            {
                while (partition.Value.Count != 0)
                {
                    T[] fullPartition = new T[Math.Min(PartitionSize, partition.Value.Count)];
                    for (int i = 0 ; i < fullPartition.Length ; i++)
                    {
                        fullPartition[i] = partition.Value.Dequeue();
                    }
                    _ReadyPartitions.Enqueue(Tuple.Create(partition.Key, fullPartition));
                }
            }
        }


        internal Queue<Tuple<string, T[]>> _ReadyPartitions = new Queue<Tuple<string, T[]>>();

        private Queue<T> GetPartition(string partition)
        {
            Queue<T> result;
            if (!_CurrentPartitions.TryGetValue(partition, out result))
            {
                result = new Queue<T>();
                _CurrentPartitions.Add(partition, result);
            }
            return result;
        }



        public bool HasFullPartition
        {
            get
            {
                return _ReadyPartitions.Count > 0;
            }
        }
    }
}
