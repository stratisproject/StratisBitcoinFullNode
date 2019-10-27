namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class TempOutput : TempRow
    {
        public string ScriptPubKey { get; set; }
        public string Address { get; set; }
        public string RedeemScript { get; set; }
        public int? OutputBlockHeight { get; set; }
        public string OutputBlockHash { get; set; }
        public int OutputTxIsCoinBase { get; set; }
        public long OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public long Value { get; set; }
        public int IsChange { get; set; }

        public TempOutput() : base() { }
    }
}
