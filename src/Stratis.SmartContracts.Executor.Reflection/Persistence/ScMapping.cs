using System;

namespace Stratis.SmartContracts.Executor.Reflection.Persistence
{
    public class ScMapping<T> : PersistenceBase, IScMapping<T>
    {
        public ScMapping(PersistentState persistentState, string name) : base(persistentState, name) { }

        public void Put(string key, T value)
        {
            if (typeof(T).IsGenericType)
            {
                throw new NotSupportedException();
            }

            this.persistentState.SetObject<T>(this.Name + "[" + key + "]", value);
        }

        public T Get(string key)
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

                if (wholeGenericType == typeof(IScList<>))
                {
                    Type genericParam = typeof(T).GetGenericArguments()[0];
                    Type mappingType = typeof(ScList<>);
                    Type genericMappingType = mappingType.MakeGenericType(genericParam);
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.persistentState, this.Name + "[" + key + "]" });
                }
            }

            return (T)this.persistentState.GetObject<T>(this.Name + "[" + key + "]");
        }

        public T this[string key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }
    }
}
