﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Tests.Common
{
    /// <summary>
    /// this class can be deleted when we solve
    /// https://stratisplatformuk.visualstudio.com/Stratis%20Full%20Node%20Backend/_workitems/edit/1174
    /// </summary>
    public class StaticFlagIsolator : IDisposable
    {
        public StaticFlagIsolator(Network network)
        {
            var isStratisNetwork = network == Network.StratisTest 
                                    || network == Network.StratisMain
                                    || network == Network.StratisRegTest;
            Transaction.TimeStamp = isStratisNetwork;
            Block.BlockSignature = isStratisNetwork;
            
        }

        public void Dispose()
        {
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }
    }
}
