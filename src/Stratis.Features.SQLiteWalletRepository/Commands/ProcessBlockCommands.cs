using SQLite;

namespace Stratis.Features.SQLiteWalletRepository.Commands
{
    internal static class ProcessBlockCommands
    {
        public static SQLiteCommand CmdUploadPrevOut(this DBConnection conn)
        {
            return conn.CreateCommand($@"
                REPLACE INTO HDTransactionData
                SELECT A.WalletID
                ,      A.AccountIndex
                ,      A.AddressType
                ,      A.AddressIndex
                ,      T.RedeemScript
                ,      T.ScriptPubKey
                ,      T.Value
                ,      T.OutputBlockHeight
                ,      T.OutputBlockHash
                ,      T.OutputTxIsCoinBase
                ,      T.OutputTxTime
                ,      T.OutputTxId
                ,      T.OutputIndex
                ,      NULL SpendTxTime
                ,      NULL SpendTxId
                ,      NULL SpendBlockHeight
                ,      NULL SpendBlockHash
                ,      NULL SpendTxIsCoinBase
                ,      NULL SpendTxTotalOut
                FROM   temp.TempOutput T
                JOIN   HDAddress A
                ON     A.ScriptPubKey = T.ScriptPubKey
                AND    A.WalletId IN (
                       SELECT WalletId
                       FROM   HDWallet
                       WHERE  IFNULL(@walletName, Name) = Name
                       AND    IFNULL(@prevHash, LastBlockSyncedHash) = LastBlockSyncedHash)
                WHERE  NOT (A.WalletID, A.AccountIndex, A.AddressType, A.AddressIndex, T.OutputTxId, T.OutputIndex) IN (
                       SELECT TD.WalletID, TD.AccountIndex, TD.AddressType, TD.AddressIndex, TD.OutputTxId, TD.OutputIndex
                       FROM   temp.TempOutput T
                       JOIN   HDTransactionData TD
                       ON     TD.OutputTxId = T.OutputTxId
                       AND    TD.OutputIndex = T.OutputIndex
                       AND    TD.ScriptPubKey = T.ScriptPubKey
                       AND    (TD.OutputBlockHash IS NOT NULL OR TD.OutputBlockHeight IS NOT NULL))");
        }

        public static SQLiteCommand CmdDeletePayments(this DBConnection conn)
        {
            return conn.CreateCommand($@"
                DELETE  FROM HDPayment
                WHERE   (OutputTxTime, OutputTxId, OutputIndex) IN (
                        SELECT TD.OutputTxTime, T.OutputTxId, T.OutputIndex
                        FROM   temp.TempPrevOut T
                        JOIN   HDTransactionData TD
                        ON     TD.OutputTxId = T.OutputTxId
                        AND    TD.OutputIndex = T.OutputIndex
                        AND    TD.SpendBlockHeight IS NULL
                        AND    TD.SpendBlockHash IS NULL
                        JOIN   HDWallet W
                        ON     W.WalletId = TD.WalletId
                        AND    IFNULL(@walletName, W.Name) = W.Name
                        AND    IFNULL(@prevHash, W.LastBlockSyncedHash) = W.LastBlockSyncedHash)");
        }

        public static SQLiteCommand CmdReplacePayments(this DBConnection conn)
        {
            return conn.CreateCommand($@"
                REPLACE INTO HDPayment
                SELECT  TD.OutputTxTime
                ,       TD.OutputTxId
                ,       TD.OutputIndex
                ,       O.OutputIndex
                ,       O.RedeemScript
                ,       O.Value
                FROM    temp.TempPrevOut T
                JOIN    temp.TempOutput O
                ON      O.OutputTxID = T.SpendTxId
                JOIN    HDTransactionData TD
                ON      TD.OutputTxId = T.OutputTxId
                AND     TD.OutputIndex = T.OutputIndex
                AND     TD.SpendBlockHeight IS NULL
                AND     TD.SpendBlockHash IS NULL
                JOIN    HDWallet W
                ON      W.WalletId = TD.WalletId
                AND     IFNULL(@walletName, W.Name) = W.Name
                AND     IFNULL(@prevHash, W.LastBlockSyncedHash) = W.LastBlockSyncedHash");
        }

        public static SQLiteCommand CmdUpdateSpending(this DBConnection conn)
        {
            return conn.CreateCommand($@"
                REPLACE INTO HDTransactionData
                SELECT TD.WalletId
                ,      TD.AccountIndex
                ,      TD.AddressType
                ,      TD.AddressIndex
                ,      TD.RedeemScript
                ,      TD.ScriptPubKey
                ,      TD.Value
                ,      TD.OutputBlockHeight
                ,      TD.OutputBlockHash
                ,      TD.OutputTxIsCoinBase
                ,      TD.OutputTxTime
                ,      TD.OutputTxId
                ,      TD.OutputIndex
                ,      T.SpendBlockHeight
                ,      T.SpendBlockHash
                ,      T.SpendTxIsCoinBase
                ,      T.SpendTxTime
                ,      T.SpendTxId
                ,      T.SpendTxTotalOut
                FROM   temp.TempPrevOut T
                JOIN   HDTransactionData TD
                ON     TD.OutputTxID = T.OutputTxId
                AND    TD.OutputIndex = T.OutputIndex
                AND    TD.SpendBlockHeight IS NULL
                AND    TD.SpendBlockHash IS NULL
                JOIN    HDWallet W
                ON      W.WalletId = TD.WalletId
                AND     IFNULL(@walletName, W.Name) = W.Name
                AND     IFNULL(@prevHash, W.LastBlockSyncedHash) = W.LastBlockSyncedHash");
        }
    }
}
