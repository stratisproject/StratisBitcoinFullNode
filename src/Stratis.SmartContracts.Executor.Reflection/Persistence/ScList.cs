using System;

namespace Stratis.SmartContracts.Executor.Reflection.Persistence
{
    public class ScList<T> : PersistenceBase, IScList<T>
    {
        public ScList(PersistentState persistentState, string name) : base(persistentState, name) { }

        public int Count
        {
            get
            {
                return persistentState.GetObject<int>(this.Name + ".Count");
            }
            set
            {
                this.persistentState.SetObject<int>(this.Name + ".Count", this.Count + 1);
            }
        }

        // TODO: this could be inefficient. Getting Count from db too many times.

        public void Push(T value)
        {
            Count++;
            Put(Count - 1, value);
        }

        public void Put(int key, T value)
        {
            if (key >= Count)
                throw new Exception("Key above list size.");

            if (typeof(T).IsGenericType)
            {
                throw new NotSupportedException();
            }

            this.persistentState.SetObject<T>(this.Name + "[" + key + "]", value);
        }

        public T Get(int key)
        {
            if (typeof(T).IsGenericType)
            {
                Type wholeGenericType = typeof(T).GetGenericTypeDefinition();
                if (wholeGenericType == typeof(IScMapping<>))
                {
                    Type genericParam = typeof(T).GetGenericArguments()[0];
                    Type mappingType = typeof(ScMapping<>);
                    Type genericMappingType = mappingType.MakeGenericType(genericParam);
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.persistentState, this.Name + "[" + key + "]" });
                }
            }

            return (T)this.persistentState.GetObject<T>(this.Name + "[" + key + "]");
        }

        public T this[int key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }
    }
}
