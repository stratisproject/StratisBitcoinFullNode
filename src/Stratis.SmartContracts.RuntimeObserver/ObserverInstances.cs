using System;
using System.Collections.Concurrent;

namespace Stratis.SmartContracts.RuntimeObserver
{
    /// <summary>
    /// Global static, used to hold all of the Observers in memory. 
    /// 
    /// TODO: Do we want to clear the list out after execution? To save node memory consumption. 
    /// </summary>
    public static class ObserverInstances
    {
        private static readonly ConcurrentDictionary<Guid, Observer> instances;

        static ObserverInstances()
        {
            instances = new ConcurrentDictionary<Guid, Observer>();
        }

        /// <summary>
        /// Gets Observer instance by id. Used by injected CIL in Rewriter so be careful if changing this. 
        /// </summary>
        public static Observer Get(string id)
        {
            return instances[Guid.Parse(id)];
        } 

        /// <summary>
        /// Set an Observer instance at a particular id. Will need to be set before execution of the observed module's code can occur. 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="observer"></param>
        public static void Set(Guid id, Observer observer)
        {
            instances[id] = observer;
        }
    }
}
