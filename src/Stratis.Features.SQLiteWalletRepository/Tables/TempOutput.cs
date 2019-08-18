namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class TempOutput : TempRow
    {
        public string PubKey { get; set; }
        public string ScriptPubKey { get; set; }
        public int OutputBlockHeight { get; set; }
        public string OutputBlockHash { get; set; }
        public int OutputTxIsCoinBase { get; set; }
        public int OutputTxTime { get; set; }
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public decimal Value { get; set; }

        public TempOutput() : base() { }
    }
}
