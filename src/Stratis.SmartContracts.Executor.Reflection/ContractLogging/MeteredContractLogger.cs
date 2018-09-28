using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    /// <summary>
    /// Injected into the <see cref="SmartContract"/> class. Spends gas for logging an event
    /// before passing it into the logger that actually handles the logging.
    /// </summary>
    public class MeteredContractLogger : IContractLogger
    {
        private readonly IGasMeter gasMeter;
        private readonly IContractLogger logger;
        private readonly Network network;
        private readonly IContractPrimitiveSerializer serializer;

        public MeteredContractLogger(IGasMeter gasMeter, IContractLogger logger, Network network, IContractPrimitiveSerializer serializer)
        {
            this.gasMeter = gasMeter;
            this.logger = logger;
            this.network = network;
            this.serializer = serializer;
        }

        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            var rawLog = new RawLog(smartContractState.Message.ContractAddress.ToUint160(this.network), toLog);
            Log log = rawLog.ToLog(this.serializer);
            this.gasMeter.Spend(GasPriceList.LogOperationCost(log.Topics, log.Data));

            // TODO: This is inefficient, it is deserializing the log more than once.
            this.logger.Log<T>(smartContractState, toLog);
        }
    }
}
