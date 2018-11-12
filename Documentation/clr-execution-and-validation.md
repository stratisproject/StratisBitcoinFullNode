# Contract Execution on the CLR

## Runtime Observer

When contracts are executing we need to have some measure of the resources that they are consuming (memory, instructions run, storage). To do this, before execution we create an instance of the `RuntimeObserver` and inject it into the contract assembly. We then inject it as a variable into every method we wish to monitor.

## Gas Tracking

To measure and limit run times, the CIL for every method in a contract is rewritten once by the `GasInjectorRewriter` so as to consume gas for every instruction run or method called. Each method is broken up into segments based on the branching instructions (occur at every closure, if, switch, loop etc.), and then a set amount of gas is spent based on the amount of computation inside each segment (calculated right now as +1 per instruction and +5 per System method call).

We also spend gas based on storage operations, these happen outside of the contract CIL though, in our own assemblies.

## Memory Consumption

To measure memory consumption and prevent an OutOfMemoryException from occurring the `MemoryLimitRewriter` also iterates through every method and checks for certain high-memory-expending calls, maintaining a rough tally of the number of objects created during execution. It monitors this for the following System calls:
* Array creation
* string.ToCharArray
* string.Split
* string.Concat
* string.Join
* string.ctor(int numberOfChars)

Depending on the exact situation, it does this by checking the input or output of the methods above. For example, array creation with an input num of 1500 will add 1500 to our internal tally, whilst retrieving a 2000-length char array from a string will check the length of the new array after it has been output and then add 2000 to our internal tally.

If the tally of created items / allocated array space goes over 10,000, execution will fail deterministically. This should prevent nodes from ever retrieving different results due to differences in memory allowances.
