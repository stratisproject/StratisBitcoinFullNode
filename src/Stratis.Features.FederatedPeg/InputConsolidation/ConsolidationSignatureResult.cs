using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    public class ConsolidationSignatureResult
    {
        /// <summary>
        /// Whether the transaction was successfully signed.
        /// </summary>
        public bool Signed { get; set; }

        /// <summary>
        /// The resulting transaction after signing.
        /// </summary>
        public Transaction TransactionResult { get; set; }

        public static ConsolidationSignatureResult Failed()
        {
            return new ConsolidationSignatureResult
            {
                Signed = false,
                TransactionResult = null
            };
        }

        public static ConsolidationSignatureResult Succeeded(Transaction result)
        {
            return new ConsolidationSignatureResult
            {
                Signed = true,
                TransactionResult = result
            };
        }
    }
}
