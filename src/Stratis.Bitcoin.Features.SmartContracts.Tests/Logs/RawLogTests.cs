﻿using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Logs
{
    public class RawLogTests
    {
        public struct Example
        {
            [Index]
            public string Name;

            [Index]
            public uint Amount;

            public Example(string name, uint amount)
            {
                this.Name = name;
                this.Amount = amount;
            }
        }

        [Fact]
        public void RawLog_With_Null_Value_Serializes()
        {
            var serializer = new ContractPrimitiveSerializer(new SmartContractPosRegTest());
            var exampleLog = new Example(null, 0);

            var rawLog = new RawLog(uint160.One, exampleLog);
            var log = rawLog.ToLog(serializer);

            Assert.Equal(3, log.Topics.Count);
            Assert.Equal(nameof(Example), Encoding.UTF8.GetString(log.Topics[0]));

            // Check that null has been serialized correctly
            Assert.Equal(new byte[0], log.Topics[1]);
            Assert.Equal(exampleLog.Amount, BitConverter.ToUInt32(log.Topics[2]));
        }
    }
}
