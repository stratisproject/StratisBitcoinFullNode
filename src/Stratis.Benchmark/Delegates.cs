using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Benchmark
{
    public class DelegateTests
    {
        public Task methodToPass(int item, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        [Benchmark]
        public void CallWithDelegate()
        {
            var x = new AsyncQueue<int>(methodToPass);
            x.Dispose();
        }

        [Benchmark]
        public void CallWithFunc()
        {
            var x = new AsyncQueue<int>((item, token) =>
            {
                return Task.CompletedTask;
            });
        }
    }
}