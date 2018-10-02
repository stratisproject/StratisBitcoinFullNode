using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using DBreeze;
using ICSharpCode.Decompiler.IL;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Patricia;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Serialization;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class PersistentStateTests
    {
        private static readonly byte[] DefaultValue = new byte[0];

        private PersistentState GetTestContext([CallerMemberName] string callingMethod = "")
        {
            var network = new SmartContractsRegTest();
            var contractPrimitiveSerializer = new ContractPrimitiveSerializer(network);
            var folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestCase", callingMethod));
            var engine = new DBreezeEngine(Path.Combine(folder, "contracts"));
            var byteStore = new DBreezeByteStore(engine, "ContractState1");
            byteStore.Empty();
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);
            var stateRepo = new StateRepositoryRoot(stateDB);
            var persistenceStrategy = new TestPersistenceStrategy(stateRepo);
            return new PersistentState(persistenceStrategy, contractPrimitiveSerializer, new uint160(0));
        }

        [Fact]
        public void PersistentState_EmptyByteArray_When_Null()
        {
            PersistentState persistentState = GetTestContext();
            Assert.Equal(DefaultValue, persistentState.GetBytes("Test"));
        }

        // TODO: Test null for everything, default values, byte arrays etc.

    }
}
