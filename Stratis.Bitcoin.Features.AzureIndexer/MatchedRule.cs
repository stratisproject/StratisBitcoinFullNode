
namespace Stratis.Bitcoin.Features.AzureIndexer
{
    public enum MatchLocation
    {
        Output,
        Input,
    }
    public class MatchedRule
    {
        public uint Index
        {
            get;
            set;
        }

        public WalletRule Rule
        {
            get;
            set;
        }

        public MatchLocation MatchType
        {
            get;
            set;
        }

        public override string ToString()
        {
            return this.Index + "-" + this.MatchType.ToString();
        }
    }
}
