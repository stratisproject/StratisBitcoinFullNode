using Stratis.SmartContracts.State;

namespace Stratis.SmartContracts
{
    public class SmartContractState
    {
        internal SmartContractState(Block block, Message message, PersistentState persistentState)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.StateRepository = this.PersistentState.StateDb; // Can change how we get this
        }

        public Block Block { get; }

        public Message Message { get; }

        public PersistentState PersistentState { get; }

        internal IContractStateRepository StateRepository { get;  }
    }
}