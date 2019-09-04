using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.TestNetworks
{
    /// <summary>
    /// Allows us to test syncing blocks around the switch from PoW to PoS.
    /// </summary>
    public class StratisMain10KCheckpoint : StratisMain
    {
        public StratisMain10KCheckpoint()
        {
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) },
                { 2, new CheckpointInfo(new uint256("0xbca5936f638181e74a5f1e9999c95b0ce77da48c2688399e72bcc53a00c61eff"), new uint256("0x7d61c139a471821caa6b7635a4636e90afcfe5e195040aecbc1ad7d24924db1e")) }, // Premine
                { 50, new CheckpointInfo(new uint256("0x0353b43f4ce80bf24578e7c0141d90d7962fb3a4b4b4e5a17925ca95e943b816"), new uint256("0x7c2af3b10d13f9d2bc6063baaf7f0860d90d870c994378144f9bf85d0d555061")) },
                { 100, new CheckpointInfo(new uint256("0x688468a8aa48cd1c2197e42e7d8acd42760b7e2ac4bcab9d18ac149a673e16f6"), new uint256("0xcf2b1e9e76aaa3d96f255783eb2d907bf6ccb9c1deeb3617149278f0e4a1ab1b")) },
                { 150, new CheckpointInfo(new uint256("0xe4ae9663519abec15e28f68bdb2cb89a739aee22f53d1573048d69141db6ee5d"), new uint256("0xa6c17173e958dc716cc0892ce33dad8bc327963d78a16c436264ceae43d584ce")) },
                { 2000, new CheckpointInfo(new uint256("0x7902db2766bb6a5a1c5b9624afc733ab2ffff5875b867d87c3b74821290aaca2"), new uint256("0x0b0b48c5ad4557b973f7c9e9e7d4fcc828c7c084e6d31b359c07c6810d24d922")) },
                { 4000, new CheckpointInfo(new uint256("0x383c951dcd8250d42141a2341dbcb449d59f87cc3b1b60d0e34765b8ebc25f41"), new uint256("0x9270895f93216d4dac1cfca1fbf5d4b3c468b719d37f9c2dbc4fe887e41fd55b")) },
                { 10000, new CheckpointInfo(new uint256("0xad873f39e811afd15aba794bd40aaeaa4843eddf193d56f4a23007834e5aefb0"), new uint256("0x895eab2f44472715e45d5ef1dab893a9c3d9860dc4c4eecd02b4c365f19bf08f")) }
            };
        }
    }
}
