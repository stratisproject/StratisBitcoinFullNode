using System;
using System.Collections.Concurrent;

namespace RuntimeObserver
{
    public static class ObserverInstances
    {
        private static readonly ConcurrentDictionary<Guid, Observer> instances;

        static ObserverInstances()
        {
            instances = new ConcurrentDictionary<Guid, Observer>();
        }

        public static Observer Get(string id)
        {
            return instances[Guid.Parse(id)];
        } 

        public static void Set(Guid id, Observer observer)
        {
            instances[id] = observer;
        }
    }
}
