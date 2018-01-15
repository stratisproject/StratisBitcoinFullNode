using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractRepositoryTests
    {
        private static readonly byte[] empty = new byte[0];
        private static readonly byte[] dog = Encoding.UTF8.GetBytes("dog");
        private static readonly byte[] dodecahedron = Encoding.UTF8.GetBytes("dodecahedron");
        private static readonly byte[] cat = Encoding.UTF8.GetBytes("cat");
        private static readonly byte[] fish = Encoding.UTF8.GetBytes("fish");
        private static readonly byte[] bird = Encoding.UTF8.GetBytes("bird");
        private static readonly uint160 testAddress = 111111;

        [Fact]
        public void IntegrationRepositoryTest()
        {
            byte[] testCode = new byte[] { 126, 127, 128, 1, 2, 3, 4, 5 };
            SmartContractStateRepository repo = new SmartContractStateRepository();
            repo.Refresh();

            repo.CreateAccount(testAddress);
            repo.SetObject(testAddress, dog, cat);
            Assert.Equal(cat, repo.GetObject<byte[]>(testAddress, dog));

            repo.SetCode(testAddress, testCode);
            Assert.Equal(testCode, repo.GetCode(testAddress));
        }

        [Fact]
        public void CommitAndRollbackTest()
        {
            SmartContractStateRepository track = new SmartContractStateRepository();
            track.Refresh();

            ISmartContractStateRepository txTrack = track.StartTracking();
            txTrack.CreateAccount(testAddress);
            txTrack.SetObject(testAddress, dog, cat);
            txTrack.Commit();
            byte[] rootTx1 = txTrack.GetRoot();
            track.LoadSnapshot(rootTx1);

            ISmartContractStateRepository txTrack2 = track.StartTracking();
            txTrack2.SetObject(testAddress, dog, fish);
            txTrack2.Rollback();

            ISmartContractStateRepository txTrack3 = track.StartTracking();
            txTrack3.SetObject(testAddress, dodecahedron, bird);
            txTrack3.Commit();
            track.LoadSnapshot(txTrack3.GetRoot());

            Assert.Equal(cat, track.GetObject<byte[]>(testAddress, dog));
            Assert.Equal(bird, track.GetObject<byte[]>(testAddress, dodecahedron));

            track.LoadSnapshot(rootTx1);
            var stuff = track.GetObject<byte[]>(testAddress, dog);
        }

        // TODO: Test commit and rollback, building in a dictionary of not-yet-flushed tries

    }
}
