﻿using System;
using System.Diagnostics;
using System.Linq;
using NBitcoin.Crypto;
using Xunit;

namespace NBitcoin.Tests
{
    class AssertEx
    {
        [DebuggerHidden]
        internal static void Error(string msg)
        {
            Assert.False(true, msg);
        }
        [DebuggerHidden]
        internal static void Equal<T>(T actual, T expected)
        {
            Assert.Equal(expected, actual);
        }
        [DebuggerHidden]
        internal static void CollectionEquals<T>(T[] actual, T[] expected)
        {
            if(actual.Length != expected.Length)
                Assert.False(true, "Actual.Length(" + actual.Length + ") != Expected.Length(" + expected.Length + ")");

            for(int i = 0; i < actual.Length; i++)
            {
                if(!Equals(actual[i], expected[i]))
                    Assert.False(true, "Actual[" + i + "](" + actual[i] + ") != Expected[" + i + "](" + expected[i] + ")");
            }
        }

        [DebuggerHidden]
        internal static void StackEquals(ContextStack<byte[]> stack1, ContextStack<byte[]> stack2)
        {
            uint256[] hash1 = stack1.Select(o => Hashes.Hash256(o)).ToArray();
            uint256[] hash2 = stack2.Select(o => Hashes.Hash256(o)).ToArray();
            CollectionEquals(hash1, hash2);
        }

        internal static void CollectionEquals(System.Collections.BitArray bitArray, int p)
        {
            throw new NotImplementedException();
        }
    }
}
