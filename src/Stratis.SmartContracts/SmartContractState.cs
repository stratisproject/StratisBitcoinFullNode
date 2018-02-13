namespace Stratis.SmartContracts
{
    public class SmartContractState
    {
        public SmartContractState(Block block, Message message, PersistentState persistentState)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
        }

        public Block Block { get; }

        public Message Message { get; }

        public PersistentState PersistentState { get; }
    }
}