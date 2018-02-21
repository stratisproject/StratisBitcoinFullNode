using Stratis.SmartContracts.Backend;
using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts
{
    /// <summary>
    /// TODO - SmartContractState is basically the same thing as SmartContractExecutionContext so merge them eventually
    /// </summary>
    public class SmartContractState
    {
        internal SmartContractState(
            Block block, 
            Message message, 
            PersistentState persistentState, 
            GasMeter gasMeter)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.StateRepository = this.PersistentState.StateDb; // Can change how we get this
            this.GasMeter = gasMeter;
        }

        public Block Block { get; }

        public Message Message { get; }

        public PersistentState PersistentState { get; }

        internal IContractStateRepository StateRepository { get;  }

        public GasMeter GasMeter { get; set; }
    }
}