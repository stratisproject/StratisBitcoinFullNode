namespace Stratis.Features.SQLiteWalletRepository.Tables
{
    internal class TempPrevOut : TempRow
    {
        public string OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public string ScriptPubKey { get; set; }
        public int? SpendBlockHeight { get; set; }
        public string SpendBlockHash { get; set; }
        public int SpendTxIsCoinBase { get; set; }
        public long SpendTxTime { get; set; }
        public string SpendTxId { get; set; }
        public int SpendIndex { get; set; }
        public long SpendTxTotalOut { get; set; }

        public TempPrevOut() : base() { }
    }
}
