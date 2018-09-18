using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection.ContractLogging
{
    public class MeteredContractLogger : IContractLogger
    {
        private readonly IGasMeter gasMeter;
        private readonly IContractLogHolder logHolder;
        private readonly Network network;
        private readonly IContractPrimitiveSerializer serializer;

        public MeteredContractLogger(IGasMeter gasMeter, IContractLogHolder logHolder, Network network, IContractPrimitiveSerializer serializer)
        {
            this.gasMeter = gasMeter;
            this.logHolder = logHolder;
            this.network = network;
            this.serializer = serializer;
        }

        public void Log<T>(ISmartContractState smartContractState, T toLog)
        {
            var rawLog = new RawLog(smartContractState.Message.ContractAddress.ToUint160(this.network), toLog);
            var log = rawLog.ToLog(this.serializer);

            // TODO: GAS METER LOGGING.

            this.logHolder.Log<T>(smartContractState, toLog);
        }
    }
}
