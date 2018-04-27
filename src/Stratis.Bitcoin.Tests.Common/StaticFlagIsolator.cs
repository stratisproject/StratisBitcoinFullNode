using System;
using NBitcoin;

namespace Stratis.Bitcoin.Tests.Common
{
    /// <summary>
    /// this class can be deleted when we solve
    /// https://stratisplatformuk.visualstudio.com/Stratis%20Full%20Node%20Backend/_workitems/edit/1174
    /// </summary>
    public class StaticFlagIsolator : IDisposable
    {
        private readonly bool previousTimeStamp;
        private readonly bool previousBlockSignature;

        public StaticFlagIsolator()
        {
            this.previousBlockSignature = Block.BlockSignature;
            this.previousTimeStamp = Transaction.TimeStamp;
        }

        public StaticFlagIsolator(Network network) : this()
        {
            var isStratisNetwork = network == Network.StratisTest || network == Network.StratisMain || network == Network.StratisRegTest;

            Transaction.TimeStamp = isStratisNetwork;
            Block.BlockSignature = isStratisNetwork;
        }

        public void Dispose()
        {
            Transaction.TimeStamp = this.previousTimeStamp;
            Block.BlockSignature = this.previousBlockSignature;
        }
    }
}