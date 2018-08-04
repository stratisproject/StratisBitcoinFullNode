namespace Stratis.SmartContracts.Executor.Reflection.Persistence
{
    public abstract class PersistenceBase
    {
        public string Name { get; }

        protected readonly PersistentState persistentState;

        public PersistenceBase(PersistentState persistentState, string name)
        {
            this.Name = name;
            this.persistentState = persistentState;
        }
    }
}
