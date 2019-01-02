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

## Validation

Contracts must be validated for two reasons:
* To ensure they do not contain sources of non-determinism
* To ensure they conform to the correct format for the protocol

Validation is performed on all contract bytecode deployed as part of a contract creation transaction. If validation fails, creation of the contract will also fail.

A contract is validated by first reading its bytecode into a module definition. The module definition is a structural representation of the .NET Assembly containing the contract type(s). Each type defined in the assembly is validated, as are its method and the instructions in those methods.

Validation works in two ways:

* Types and members are validated against a whitelist. Types and members must be explicitly whitelisted or they cannot be used in a contract.
* Types, methods and instructions are validated with individual validators.

### Determinism Validation

Nodes must be capable of reaching consensus on the execution result of a contract. Sources of non-determinism prevent this from occurring and are not permitted in a contract.

For example, consider a contract method that uses `System.DateTime.Now`. A node can execute a contract transaction at any point in time, meaning the output of `System.DateTime.Now` changes - it is non-deterministic. Because nodes can fail to reach consensus on this value, contracts that use `System.DateTime` will fail validation.

Validation of types and members used in an assembly occurs against a whitelist. Any usages of a type or member that is not in the whitelist will cause validation to fail.

#### Whitelisted Types and Members

| Namespace                       | Type                          | Member          |
|---------------------------------|-------------------------------|-----------------|
| System                          | Boolean                       |                 |
| System                          | Byte                          |                 |
| System                          | Char                          |                 |
| System                          | Int32                         |                 |
| System                          | UInt32                        |                 |
| System                          | Int64                         |                 |
| System                          | UInt64                        |                 |
| System                          | String                        |                 |
| System                          | Void                          |                 |
| System                          | Array                         | GetLength       |
| System                          | Array                         | Copy            |
| System                          | Array                         | GetValue        |
| System                          | Array                         | SetValue        |
| System                          | Array                         | Resize          |
| System                          | Object                        | ToString        |
| System.Runtime.CompilerServices | IteratorStateMachineAttribute |                 |
| System.Runtime.CompilerServices | RuntimeHelpers                | InitializeArray |
| Stratis.SmartContracts          |                               |                 |
| Stratis.SmartContracts          | SmartContract                 |                 |

#### Blacklisted CLR Opcodes

The following CLR opcodes are blacklisted due to use of floating point numbers, which are non-deterministic in the CLR.

* [Ldc_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldc_r4)
* [Ldc_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldc_r8)
* [Ldelem_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldelem_r4)
* [Ldelem_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldelem_r8)
* [Conv_R_Un](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.conv_r_un)
* [Conv_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.conv_r4)
* [Conv_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.conv_r8)
* [Ldind_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldind_r4)
* [Ldind_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.ldind_r8)
* [Stelem_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stelem_r4)
* [Stelem_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stelem_r8)
* [Stind_R4](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stind_r4)
* [Stind_R8](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.stind_r8)

#### Other Source of Non-Determinism
* Finalizers are not allowed. Giving a method the name `Finalize` will cause validation to fail.
* Exception handling with `try/catch` blocks is not allowed. Exceptions contain stack traces that are a source of non-determinism.

### Format Validation

Contracts are validated for adherence to the correct format. Rules for the expected format of a contract are defined for technical reasons.

| Format Validator Type           | Description                                                                                                                                                                                                                                                                                                          |
|---------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Assembly References             | Assembly must not reference forbidden assemblies                                                                                                                                                                                                                                                                     |
| Deploy Attribute                | If multiple contracts are present, only one of them must contain a `[Deploy]` attribute                                                                                                                                                                                                                              |
| Static Constructor              | No static constructors can used. This includes a static constructor definition as well as statically initialized fields. The reason this is not allowed is that we have no control over static constructor invocation in the CLR, meaning that contracts could be in an invalid state.                               |
| Generic Type                    | A declared type must not have generic parameters                                                                                                                                                                                                                                                                     |
| Namespace                       | A type must not use a namespace                                                                                                                                                                                                                                                                                      |
| Single constructor              | A contract must declares a single constructor only                                                                                                                                                                                                                                                                   |
| First constructor param         | The first parameter of a contract's constructor must be an `ISmartContractState` type                                                                                                                                                                                                                                |
| Inherits `SmartContract`        | A contract must inherit from `Stratis.SmartContracts.SmartContract`                                                                                                                                                                                                                                                  |
| Field definition                | A contract must not define any fields. Fields are not persisted and only retain their value during the execution scope of a transaction. They would therefore behave the same as a local variable.                                                                                                                   |
| Nested type declares methods    | A nested type must not declare any methods                                                                                                                                                                                                                                                                           |
| Multiple levels of nested types | A nested type must not declare a nested type                                                                                                                                                                                                                                                                         |
| Try-catch                       | A contract must not use a try-catch block                                                                                                                                                                                                                                                                            |
| Method param types              | A public method must only have parameters that are one of these types: * System.Boolean * System.Byte * System.Char * System.String * System.UInt32 * System.Int32 * System.UInt64 * System.Int64 * System.Byte[] * Stratis.SmartContracts.Address  A private method can additionally contain: * ValueTypes * Arrays |
| Optional method params          | A method must not contain any optional parameters                                                                                                                                                                                                                                                                    |
| Generic method param            | A method must not have generic parameters                                                                                                                                                                                                                                                                            |
| Multidimensional array          | A contract must not use a multidimensional array                                                                                                                                                                                                                                                                     |
| New object validator            | A contract must not use [NewObj](https://docs.microsoft.com/en-US/dotnet/api/system.reflection.emit.opcodes.newobj)                                                                                                                                                                                                                                                                                        |