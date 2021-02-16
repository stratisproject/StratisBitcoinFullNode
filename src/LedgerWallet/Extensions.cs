using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet
{
    public static class Extensions
    {
        internal static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            using(var delayCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var waiting = Task.Delay(-1, delayCTS.Token);
                var doing = task;
                await Task.WhenAny(waiting, doing);
                delayCTS.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        internal static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            using(var delayCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var waiting = Task.Delay(-1, delayCTS.Token);
                var doing = task;
                await Task.WhenAny(waiting, doing);
                delayCTS.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return await doing;
            }
        }
        public static Dictionary<TKey, TValue> ToDictionaryUnique<TKey, TValue>(this IEnumerable<TValue> v, Func<TValue, TKey> selectKey)
        {
            var dico = new Dictionary<TKey, TValue>();
            foreach(var value in v)
            {
                var k = selectKey(value);
                if(!dico.ContainsKey(k))
                    dico.Add(k, value);
            }
            return dico;
        }
    }
}
