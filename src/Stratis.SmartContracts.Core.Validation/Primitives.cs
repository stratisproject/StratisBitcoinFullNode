using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Validation
{
    public static class Primitives
    {
        public static IEnumerable<Type> Types { get; } = new []
        {
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(char),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(string)
        };
    }
}