using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stratis.Bitcoin.IntegrationTests.Wallet;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            RunAllTestsOf<WalletTests>();
            RunAllTestsOf<NodeSyncTests>();
        }

        public static void RunAllTestsOf<T>()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            var testables =
            (
                from type in assembly.GetTypes().Where(t => t == typeof(T))
                where type.GetConstructor(Type.EmptyTypes) != null
                from method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                where method.GetCustomAttributes<FactAttribute>().Any()
                select new { type, method }
            );

            var executed = new Dictionary<MethodInfo, (Type, Exception)>();

            foreach (var testable in testables)
            {
                try
                {
                    object classToTest = Activator.CreateInstance(testable.type);

                    testable.method.Invoke(classToTest, new object[] { });

                    executed.Add(testable.method, (testable.type, null));
                }
                catch (Exception e)
                {
                    executed.Add(testable.method, (testable.type, e));
                }
            }

            foreach (KeyValuePair<MethodInfo, (Type, Exception)> item in executed)
            {
                Console.WriteLine(item.Value.Item2 == null ? "+" : "-  " + item.Value.Item1.Name + " " + item.Key.Name);
            }
        }
    }
}
