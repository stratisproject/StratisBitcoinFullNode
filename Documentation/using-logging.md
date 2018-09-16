# Using Logging

We are using NLog. There are two things that you need to understand - configuration of logging and logging itself. Starting with the latter, that is the simple part. However, in this document, 
we only describe what you need to do to make the logging work. There is a separate document that describes [the style of the logging we use](./logging-style.md).

## Adding Logging Code

You will use a class logger or an instance logger (or even both in the same class). A class logger is used to log in static methods. An instance logger is more common and that is used when you log inside non-static methods. 


### Creating Instance Logger

You always need to pass `ILoggerFactory` to the class constructor (or get it from DI) and then you can create an instance logger as follows:

```
/// <summary>Instance logger.</summary>
private readonly ILogger logger;
...
this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
```

And that's it! Note that you don't want to pass ILogger, always pass ILoggerFactory. It is NLog's job to filter which logs do you want to see and which you don't. 
For more information on that, see [Logging Configuration](#logging-configuration) below.


### Creating Class Logger

Class logger is little more difficult as you may not have access to any factory. So you will have to create and initialize a new one:

```
ILoggerFactory loggerFactory = new LoggerFactory();
loggerFactory.AddConsole();  // Only if you want to include logging to the console.
loggerFactory.AddNLog();   // Only if you want to include logging to the disk. This requires using NLog.Extensions.Logging.

```

Then you can create a class logger as follows:

```
/// <summary>Class logger.</summary>
private static readonly ILogger logger;
...
logger = loggerFactory.CreateLogger(typeof(YourClassName).FullName);
```


### Using Loggers

Both types of loggers are used in a same way via `Microsoft.Extensions.Logging` and `ILogger` interface.
Use `LogTrace`, `LogDebug`, `LogInformation`, `LogWarning`, `LogError`, and `LogCritical` methods on your logger:

```
// Instance logger on trace level:
this.logger.LogTrace("Message: {0}", message);
// Class logger on warning level:
logger.LogWarning("Message: {0}", message);
```

#### Interpolated Strings

Do not use [interpolated strings](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interpolated-strings) with loggers. 
The reason is that the loggers themselves integrate ability to provide composite format string and array of objects to format. This allows the logger 
to avoid the performance penalty of formatting the string if the particular log is below the currently enabled level for the given class.
For example, consider the following code:

```
this.logger.LogTrace($"Message: {message}");
```

If the logging level is strictly above TRACE, this call makes a useless operation of formatting the interpolated string that is passed 
to the logger, which immediately returns because it recognizes that the TRACE level should not be logged. If you instead use the following code:

```
this.logger.LogTrace("Message: {0}", message);
```

you avoid the penalty because the logger first checks the logging level and it returns immediately if the message should not be logged.
Only if it should be logged, it formats the final message itself and uses it.


## Logging Configuration


### Default logging

NLog defines the minimum log level to be logged for every class. If you run the node with default settings, information level is the minimum log level for all classes 
and all matching logs go to both console and the main log file. The main log file is `node.txt` under `$DATADIR/Logs` directory. This log is rotating, which means 
that every day there is a new log file and the old one is moved to `node-YYYY-MM-DD.N.txt` using the current time stamp. This means that the log file created on 31st July 2017 
will go to `node-2017-08-01.1.txt` (not to `node-2017-07-31.1.txt`). Logs from last 7 days are archived. If there are more than 7 old log files, the oldest one 
will be deleted.


### Debug Option

You can specify `-debug` command line option, which works similarly to how it works in Bitcoin Core. This enables logging of selected or all classes on DEBUG level. 
To enable DEBUG level for all classes, you use `-debug=1`. If you want to enable DEBUG level just for some classes, you use `-debug=pattern1,pattern2,...`. 
There are several shortcuts preset for most common components. For example `rpc` is a shortcut pattern for `Stratis.Bitcoin.Features.RPC.*`. This means setting `-debug=rpc` is 
the same as setting `-debug=Stratis.Bitcoin.Features.RPC.*`, which means that all classes under `Stratis.Bitcoin.Features.RPC` namespaces will log on DEBUG level. Similarly, 
you can use `consensus` as a shortcut for `Stratis.Bitcoin.Features.Consensus.*`. The full list of shortcuts is available in `Stratis.Bitcoin.Logging.LogsExtension.cs`. 
If a shortcut does not exist, you can simply use the full name of a class or a namespace. You have to use `.*` suffix for namespaces to include all its classes.


### NLog.config

If you don't want to use the debug option, or you need a more complex set of rules, you can use an `NLog.config` file. You will need to create the `NLog.config` file and place it one of these locations:
* in the folder where you have your DLL files - e.g. `Stratis.Bitcoin.dll`. The path to this folder is usually something like `StratisBitcoinFullNode/Stratis.BitcoinD/bin/Debug/netcoreapp1.1`, or
* in the `$DATADIR` directory

Here is the very basic `NLog.config` configuration file that you can start with:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target xsi:type="File" name="debugAllFile" fileName="debug.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    <target xsi:type="null" name="null" formatMessage="false" /> 
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <logger name="*" minlevel="Trace" writeTo="debugAllFile" />
  </rules>
</nlog>
```

This will create a separated `debug.txt` log file inside `$DATADIR/Logs` directory (see [debug* Targets](#debug-targets) section below) and will set all classes to log on trace level to this file.
This file and its rules are completely separated from logging to console and `node.txt`. So `node.txt` can be understood as a production or users' log file,
and `debug.txt` is a developers' log file. 

`autoReload="true"` setting makes it possible to modify the contents of `NLog.config` without the need to stop the node's process. When you save the changes of the config file, 
the rules will be automatically applied to the logging configuration inside the running application.

The `null` rule is there to prevent logs to be created before logging is initialized in `NodeSettings` class. This is to prevent creating `debug.txt` outside `$DATADIR` folder. 
If you need to see logs before the logging is initialized, you can delete this rule from `NLog.config` and you will see the logs in `debug.txt` created in the working directory 
of the application (but only if the application has write access to that folder). However, as the logging gets initialized, a new `debug.txt` will be created inside `$DATADIR` folder,
so you will have 2 separated `debug.txt` files - the first one will contain logs from before the initialization and the second one will contain all logs after the initialization.

You can create whatever rules you want in the config file, just note that the shortcuts that were available for debug option do not work here, so a rule like this

```
    <logger name="rpc" minlevel="Trace" writeTo="debugAllFile" />
```

will not work. In `NLog.config`, you need to define this rule as follows:

```
    <logger name="Stratis.Bitcoin.Features.RPC.*" minlevel="Trace" writeTo="debugAllFile" />
```


The NLog configuration file is very rich, please visit [NLog documentation](https://github.com/nlog/NLog/wiki/Configuration-file) for more information if you are not familiar with it
or ask developers on Stratis Slack. With NLog you have many different targets and rules and those targets do not just need to be files on disk. However, note that our logging 
into the console does not go through NLog. If you create rules that go to console via NLog, you will see a mix of console logs from `Microsoft.Extensions.Logging` 
and NLog.

Note that in general, having TRACE level logs enabled is not recommended if you need to go through IBD phase because it could significantly prolong it.


#### debug* Targets

We have special handling of logging targets in `NLog.config` file whose names start with `debug` prefix. All such targets have to have `xsi:type` set to either `AsyncWrapper` (see [Async Wrapper](#async-wrapper) section below)
or to `File` and their `fileName` has to define a relative path to a log file, it must not be an absolute path! The path is relative to `$DATADIR/Logs` directory. Paths of other targets, without `debug` prefix, 
are not altered in any way.


#### Async Wrapper

It is often useful not to use async logging during the development because if the program crashes, you might not have all logs flushed to the disk and you might miss important logs 
related to the crash. However, if you are testing features and do not expect crashes, you might want to use async wrapper to speed things up - especially, if you have a lot of logs.
You can also mix async wrappers with non-async targets in a way that you use async for all components that you don't expect to crash, while you use non-async targets for those that 
are under your active development and may crash.

Here is the basic `NLog.config` configuration file with async wrapper:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target name="debugAllFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="debug.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target xsi:type="null" name="null" formatMessage="false" /> 
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <logger name="*" minlevel="Trace" writeTo="debugAllFile" />
  </rules>
</nlog>
```

In case you are in control of the exception that crashes the program and you want to avoid losing logs by flushing manually using `NLog.LogManager.Flush()`.


#### Advanced Configuration File Sample

Here we have two more advanced examples of `NLog.config` file. The first one fully logs everything to `debug.txt` file, but it also creates separated files 
for logs of some components:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target name="debugAllFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="debug.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugBlockPullerFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="blockpuller.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugBlockStoreFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="blockstore.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugCoinViewsFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="coinview.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugMiningValidationFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="miner.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugTimeSyncFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="timesync.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target name="debugWalletFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="wallet.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target xsi:type="null" name="null" formatMessage="false" /> 
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <logger name="Stratis.Bitcoin.BlockPulling.*" minlevel="Trace" writeTo="debugBlockPullerFile" />

    <logger name="Stratis.Bitcoin.Features.BlockStore.*" minlevel="Trace" writeTo="debugBlockStoreFile" />

    <logger name="Stratis.Bitcoin.Features.Consensus.CoinViews.*" minlevel="Trace" writeTo="debugCoinViewsFile" />

    <logger name="Stratis.Bitcoin.Features.Consensus.ConsensusLoop" minlevel="Trace" writeTo="debugMiningValidationFile" />
    <logger name="Stratis.Bitcoin.Features.Consensus.StakeValidator" minlevel="Trace" writeTo="debugMiningValidationFile" />
    <logger name="Stratis.Bitcoin.Features.Consensus.PosConsensusValidator" minlevel="Trace" writeTo="debugMiningValidationFile" />
    <logger name="Stratis.Bitcoin.Features.Consensus.PowConsensusValidator" minlevel="Trace" writeTo="debugMiningValidationFile" />
    <logger name="Stratis.Bitcoin.Features.Miner.*" minlevel="Trace" writeTo="debugMiningValidationFile" />

    <logger name="Stratis.Bitcoin.Base.TimeSyncBehaviorState" minlevel="Trace" writeTo="debugTimeSyncFile" />
    <logger name="Stratis.Bitcoin.Base.TimeSyncBehavior" minlevel="Trace" writeTo="debugTimeSyncFile" />

    <logger name="Stratis.Bitcoin.Features.Wallet.*" minlevel="Trace" writeTo="debugWalletFile" />

    <logger name="*" minlevel="Trace" writeTo="debugAllFile" />
  </rules>
</nlog>
```

This first example can be used as a template, from which we can derive more verbose config files.

Verbose config file can be found in [here](./NLog.config).