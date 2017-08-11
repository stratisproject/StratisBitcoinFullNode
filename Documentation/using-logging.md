# Using Logging

We are using NLog. There are two things that you need to understand - configuration of logging and logging itself. Starting with the latter, that is the simple part.

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


## Logging Configuration


### Default logging

NLog defines the minimum log level to be logged for every class. If you run the node with default settings, information level is the minimum log level for all classes 
and all matching logs go to both console and the main log file. The main log file is `node.txt` under `$DATADIR/Logs` directory. This log is rotating, which means 
that every day there is a new log file and the old one is moved to `node-YYYY-MM-DD.N.txt` using the current time stamp. This means that the log file created on 31st July 2017 
will go to `node-2017-08-01.1.txt` (not to `node-2017-07-31.1.txt`). Logs from last 7 days are archived. If there are more than 7 old log files, the oldest one 
will be deleted.


### Debug Option

You can specify `-debug` command line option, which works similarly to how it works in Bitcoin Core. This enables logging of selected or all classes on trace level and this 
setting is only for the main log file, not the console. To enable trace level for all classes, you use `-debug=1`. If you want to enable trace level just for some classes, you use `-debug=pattern1,pattern2,...`. 
There are several shortcuts preset for most common components. For example `rpc` is a shortcut pattern for `Stratis.Bitcoin.Features.RPC.*`. This means setting `-debug=rpc` is 
the same as setting `-debug=Stratis.Bitcoin.Features.RPC.*`, which means that all classes under `Stratis.Bitcoin.Features.RPC` namespaces will log on trace level. Similarly, 
you can use `consensus` as a shortcut for `Stratis.Bitcoin.Features.Consensus.*`. The full list of shortcuts is available in `Stratis.Bitcoin.Logging.LogsExtension.cs`. 
If a shortcut does not exist, you can simply use the full name of a class or a namespace. You have to use `.*` suffix for namespaces to include all its classes.


### NLog.config

If you don't want to use the debug option, or you need a more complex set of rules, you can use `NLog.config` file. You need to create `NLog.config` file in the folder where 
you have your DLL files - e.g. `Stratis.Bitcoin.dll`. The path to this folder is usually something like `StratisBitcoinFullNode/Stratis.BitcoinD/bin/Debug/netcoreapp1.1`.

Here is the very basic `NLog.config` configuration file that you should start with:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target xsi:type="File" name="debugFile" fileName="debug.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    <target xsi:type="null" name="null" formatMessage="false" /> 
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <logger name="*" minlevel="Trace" writeTo="debugFile" />
  </rules>
</nlog>
```

This will create a separated `debug.txt` log file inside `$DATADIR/Logs` directory and will set all classes to log on trace level to this file.
This file and its rules are completely separated from logging to console and `node.txt`. So `node.txt` can be understood as a production or users' log file,
and `debug.txt` is developers' log file. 

`autoReload="true"` setting makes it possible to modify the contents of `NLog.config` without the need to stop the node's process. When you save the changes of the config file, 
the rules will be automatically applied to the logging configuration inside the running application.

The `null` rule is there to prevent logs to be created before logging is initialized in `NodeSettings` class. This is to prevent creating `debug.txt` outside `$DATADIR` folder. 
If you need to see logs before the logging is initialized, you can delete this rule from `NLog.config` and you will see the logs in `debug.txt` created in the working directory 
of the application (but only if the application has write access to that folder). However, as the logging gets initialized, a new `debug.txt` will be created inside `$DATADIR` folder,
so you will have 2 separated `debug.txt` files - the first one will contain logs from before the initialization and the second one will contain all logs after the initialization.

You can create whatever rules you want in the config file, just note that the shortcuts that were available for debug option do not work here, so a rule like this

```
    <logger name="rpc" minlevel="Trace" writeTo="debugFile" />
```

will not work. In `NLog.config`, you need to define this rule as follows:

```
    <logger name="Stratis.Bitcoin.Features.RPC.*" minlevel="Trace" writeTo="debugFile" />
```


The NLog configuration file is very rich, please visit [NLog documentation](https://github.com/nlog/NLog/wiki/Configuration-file) for more information if you are not familiar with it
or ask developers on Stratis Slack. With NLog you have many different targets and rules and those targets do not just need to be files on disk. However, note that our logging 
into the console does not go through NLog. If you create rules that go to console via NLog, you will see a mix of console logs from `Microsoft.Extensions.Logging` 
and NLog.


#### Async Wrapper

It is often useful not to use async logging during the development because if the program crashes, you might not have all logs flushed to the disk and you might miss important logs 
related to the crash. However, if you are testing features and do not expect crashes, you might want to use async wrapper to speed things up - especially, if you have a lot of logs.

Here is the basic `NLog.config` configuration file with async wrapper:

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target name="debugFile" xsi:type="AsyncWrapper" queueLimit="10000" overflowAction="Block" batchSize="1000">
      <target xsi:type="File" fileName="debug.txt" layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}" encoding="utf-8" /> 
    </target>
    <target xsi:type="null" name="null" formatMessage="false" /> 
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <logger name="*" minlevel="Trace" writeTo="debugFile" />
  </rules>
</nlog>
```

In case you are in control of the exception that crashes the program and you want to avoid losing logs by flushing manually using `NLog.LogManager.Flush()`.
