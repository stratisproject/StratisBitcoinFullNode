using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class UnconfirmedBalanceLocator : BalanceLocator
    {
        internal const int UnconfHeight = (int.MaxValue - 1);
        public UnconfirmedBalanceLocator()
        {
            SeenDate = Utils.UnixTimeToDateTime(0);
        }
        public UnconfirmedBalanceLocator(DateTimeOffset seenDate, uint256 transactionId = null)
        {
            SeenDate = seenDate;
            TransactionId = transactionId;
        }
        public new static UnconfirmedBalanceLocator Parse(string str)
        {
            var result = BalanceLocator.Parse(str);
            if (!(result is UnconfirmedBalanceLocator))
                throw InvalidUnconfirmedBalance();
            return (UnconfirmedBalanceLocator)result;
        }

        private static FormatException InvalidUnconfirmedBalance()
        {
            return new FormatException("Invalid Unconfirmed Balance Locator");
        }

        public DateTimeOffset SeenDate
        {
            get;
            set;
        }

        public uint256 TransactionId
        {
            get;
            set;
        }

        internal static BalanceLocator ParseCore(string[] splitted, bool queryFormat)
        {
            if (splitted.Length < 2)
                throw InvalidUnconfirmedBalance();

            uint date = ParseUint(splitted[1]);
            uint256 transactionId = null;
            if (splitted.Length >= 3)
                transactionId = new uint256(Encoders.Hex.DecodeData(splitted[2]), false);
            return new UnconfirmedBalanceLocator(Utils.UnixTimeToDateTime(date), transactionId);
        }

        private static uint ParseUint(string str)
        {
            uint result;
            if (!uint.TryParse(str, out result))
                throw InvalidUnconfirmedBalance();
            return result;
        }

        public override string ToString(bool queryFormat)
        {
            var height = queryFormat ? Helper.HeightToString(UnconfHeight) : (UnconfHeight).ToString();
            var date = ToString(SeenDate);
            return height + "-" + date+ "-" + TransactionId;
        }

        private string ToString(DateTimeOffset SeenDate)
        {
            return Utils.DateTimeToUnixTime(SeenDate).ToString(Helper.format);
        }

        public override BalanceLocator Ceil()
        {
            UnconfirmedBalanceLocator result = this;
            if (TransactionId == null)
                result = new UnconfirmedBalanceLocator(result.SeenDate, transactionId: _MinUInt256);
            return result;
        }

        public override BalanceLocator Floor()
        {
            UnconfirmedBalanceLocator result = this;
            if (TransactionId == null)
                result = new UnconfirmedBalanceLocator(result.SeenDate, transactionId: _MaxUInt256);
            return result;
        }
    }
    public class ConfirmedBalanceLocator : BalanceLocator
    {
        public new static ConfirmedBalanceLocator Parse(string str)
        {
            var result = BalanceLocator.Parse(str);
            if (!(result is ConfirmedBalanceLocator))
                throw new FormatException("Invalid Confirmed Balance Locator");
            return (ConfirmedBalanceLocator)result;
        }
        internal static BalanceLocator ParseCore(int height, string[] splitted)
        {
            uint256 blockId = null;
            uint256 transactionId = null;

            if (splitted.Length >= 2)
                blockId = new uint256(Encoders.Hex.DecodeData(splitted[1]), false);
            if (splitted.Length >= 3)
                transactionId = new uint256(Encoders.Hex.DecodeData(splitted[2]), false);
            return new ConfirmedBalanceLocator(height, blockId, transactionId);
        }
        public ConfirmedBalanceLocator()
        {

        }
        public ConfirmedBalanceLocator(OrderedBalanceChange change)
            : this(change.Height, change.BlockId, change.TransactionId)
        {
        }
        public ConfirmedBalanceLocator(int height, uint256 blockId = null, uint256 transactionId = null)
        {
            if (height >= UnconfirmedBalanceLocator.UnconfHeight)
                throw new ArgumentOutOfRangeException("height", "A confirmed block can't have such height");
            Height = height;
            BlockHash = blockId;
            TransactionId = transactionId;
        }
        public int Height
        {
            get;
            set;
        }
        public uint256 BlockHash
        {
            get;
            set;
        }
        public uint256 TransactionId
        {
            get;
            set;
        }

        public override string ToString(bool queryFormat)
        {
            var height = queryFormat ? Helper.HeightToString(Height) : Height.ToString();
            return height + "-" + BlockHash + "-" + TransactionId;
        }

        public override BalanceLocator Floor()
        {
            ConfirmedBalanceLocator result = this;
            if (TransactionId == null)
                result = new ConfirmedBalanceLocator(result.Height, result.BlockHash, transactionId: _MinUInt256);

            if (BlockHash == null)
                result = new ConfirmedBalanceLocator(result.Height, _MinUInt256, result.TransactionId);
            return result;
        }

        public override BalanceLocator Ceil()
        {
            ConfirmedBalanceLocator result = this;
            if (TransactionId == null)
                result = new ConfirmedBalanceLocator(result.Height, result.BlockHash, _MaxUInt256);

            if (BlockHash == null)
                result = new ConfirmedBalanceLocator(result.Height, _MaxUInt256, result.TransactionId);
            return result;
        }
    }

    public abstract class BalanceLocator
    {
        static BalanceLocator()
        {
            _MinUInt256 = new uint256(new byte[32]);
            var b = new byte[32];
            for (int i = 0 ; i < b.Length ; i++)
            {
                b[i] = 0xFF;
            }
            _MaxUInt256 = new uint256(b);
        }
        internal static uint256 _MinUInt256;
        internal static uint256 _MaxUInt256;
        public static BalanceLocator Parse(string str)
        {
            return Parse(str, false);
        }
        public static BalanceLocator Parse(string str, bool queryFormat)
        {
            var splitted = str.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries);
            if (splitted.Length == 0)
                throw new FormatException("Invalid BalanceLocator string");
            var height = queryFormat ? Helper.StringToHeight(splitted[0]) : int.Parse(splitted[0]);

            if (height == UnconfirmedBalanceLocator.UnconfHeight)
            {
                return UnconfirmedBalanceLocator.ParseCore(splitted, queryFormat);
            }
            else
            {
                return ConfirmedBalanceLocator.ParseCore(height, splitted);
            }
        }


        public override string ToString()
        {
            return ToString(false);
        }

        public abstract string ToString(bool queryFormat);

        public bool IsGreaterThan(BalanceLocator to)
        {
            var result = string.Compare(ToString(true), to.ToString(true));
            return result < 1;
        }

        public abstract BalanceLocator Floor();
        public abstract BalanceLocator Ceil();
    }

    public class BalanceQuery
    {
        public BalanceQuery()
        {
            From = new UnconfirmedBalanceLocator();
            To = new ConfirmedBalanceLocator(0);
            ToIncluded = true;
            FromIncluded = true;
        }
        public BalanceLocator To
        {
            get;
            set;
        }
        public bool ToIncluded
        {
            get;
            set;
        }

        public BalanceLocator From
        {
            get;
            set;
        }

        public bool FromIncluded
        {
            get;
            set;
        }

        public bool RawOrdering
        {
            get;
            set;
        }

        public IEnumerable<int> PageSizes
        {
            get;
            set;
        }


        public TableQuery CreateTableQuery(BalanceId balanceId)
        {
            return CreateTableQuery(balanceId.PartitionKey, balanceId.ToString());
        }

        public TableQuery CreateTableQuery(string partitionId, string scope)
        {
            BalanceLocator from = From ?? new UnconfirmedBalanceLocator();
            BalanceLocator to = To ?? new ConfirmedBalanceLocator(0);
            var toIncluded = ToIncluded;
            var fromIncluded = FromIncluded;

            //Fix automatically if wrong order
            if (!from.IsGreaterThan(to))
            {
                var temp = to;
                var temp2 = toIncluded;
                to = from;
                toIncluded = FromIncluded;
                from = temp;
                fromIncluded = temp2;
            }
            ////

            //Complete the balance locator is partial
            from = fromIncluded ? from.Floor() : from.Ceil();
            to = toIncluded ? to.Ceil() : to.Floor();
            /////

            return new TableQuery()
            {
                FilterString =
                TableQuery.CombineFilters(
                                            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionId),
                                            TableOperators.And,
                                            TableQuery.CombineFilters(
                                            TableQuery.GenerateFilterCondition("RowKey",
                                                    fromIncluded ? QueryComparisons.GreaterThanOrEqual : QueryComparisons.GreaterThan,
                                                    scope + "-" + from.ToString(true)),
                                                TableOperators.And,
                                                TableQuery.GenerateFilterCondition("RowKey",
                                                        toIncluded ? QueryComparisons.LessThanOrEqual : QueryComparisons.LessThan,
                                                        scope + "-" + to.ToString(true))
                                            ))
            };
        }

    }
}
