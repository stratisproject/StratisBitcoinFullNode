using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.SmartContracts.CLR.ContractLogging
{
    /// <summary>
    /// Injected into the <see cref="SmartContract"/> class. Spends gas for logging an event
    /// before passing it into the logger that actually handles the logging.
    /// </summary>
    public class MeteredContractLogger : IContractLogger
    {
        private readonly RuntimeObserver.IGasMeter gasMeter;
        private readonly IContractLogger logger;
        private readonly IContractPrimitiveSerializer serializer;

        public MeteredContractLogger(RuntimeObserver.IGasMeter gasMeter, IContractLogger logger, IContractPrimitiveSerializer serializer)
        {
            this.gasMeter = gasMeter;
            this.logger = logger;
            this.serializer = serializer;
        }

        public void Log<T>(ISmartContractState smartContractState, T toLog) where T : struct 
        {
            var rawLog = new RawLog(smartContractState.Message.ContractAddress.ToUint160(), toLog);
            Log log = rawLog.ToLog(this.serializer);
            this.gasMeter.Spend(GasPriceList.LogOperationCost(log.Topics, log.Data));

            // TODO: This is inefficient, it is deserializing the log more than once.
            this.logger.Log<T>(smartContractState, toLog);
        }
    }
}
