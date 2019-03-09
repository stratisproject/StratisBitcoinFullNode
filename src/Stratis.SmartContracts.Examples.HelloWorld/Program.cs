using System;

namespace Stratis.SmartContracts.Examples.HelloWorld
{
    // Just a example class containing a Main function so the project can build.
    // If the project builds here, the C# syntax in the files is, at least, not erroneous.
    // The "Hello World" cs files still have to be built by the
    // Stratis sct tool to verify they are non-deterministic and to access the CIL. 
    class Program
    {
        static void Main(string[] args)
        {
            // Non Smart Contract "Hello World" added by IDE! 
            Console.WriteLine("Hello World!");
        }
    }
}