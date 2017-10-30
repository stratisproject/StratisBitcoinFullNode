using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class ChainPartEntry
    {
        public ChainPartEntry()
        {
            BlockHeaders = new List<BlockHeader>();
        }

        public ChainPartEntry(DynamicTableEntity entity)
        {
            ChainOffset = Helper.StringToHeight(entity.RowKey);
            BlockHeaders = new List<BlockHeader>();         
            foreach (var prop in entity.Properties)
            {
                var header = new BlockHeader();
                header.FromBytes(prop.Value.BinaryValue);
                BlockHeaders.Add(header);
            }
        }

        public int ChainOffset
        {
            get;
            set;
        }

        public List<BlockHeader> BlockHeaders
        {
            get;
            private set;
        }

        public BlockHeader GetHeader(int height)
        {
            if (height < ChainOffset)
                return null;
            height = height - ChainOffset;
            if (height >= BlockHeaders.Count)
                return null;
            return BlockHeaders[height];
        }

        public DynamicTableEntity ToEntity()
        {
            DynamicTableEntity entity = new DynamicTableEntity();
            entity.PartitionKey = "a";
            entity.RowKey = Helper.HeightToString(ChainOffset);
            int i = 0;
            foreach (var header in BlockHeaders)
            {
                entity.Properties.Add("a" + i, new EntityProperty(header.ToBytes()));
                i++;
            }
            return entity;
        }
    }
}
