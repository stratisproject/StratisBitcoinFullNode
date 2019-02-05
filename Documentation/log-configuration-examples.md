# NLog configuration examples

This document shows a list of useful NLog.config configurations we are using internally for our tests.


## Full Consensus Log

When we need to track down the reason of bugs that involve the Consensus layer, we need to enable Consensus trace logging.

Consensus layer is one of the most important layer in our FullNode implementation and it generate lot of tracing.  
Enabling trace on every consensus related component will generate huge log files.

In this example, we are logging every Consensus related trace, archiving them in a file called debug.txt in our node datafolder/logs path.
Since the log can be quite huge, we are generating archives when the log files reaches the size of 250MB.

In case you want to limit the maximum available space taken by logs, you can use the the `maxArchiveFiles` parameter to specify how many archive
files you want to keep at maximum.
If you don't want to exceed 2 GB of space for logs, you can set the `maxArchiveFiles` to 7, this way log files will take at max 250x7 archive plus current log file.

```
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" autoReload="true">
  <targets>
    <target xsi:type="File"
        name="debugFile"
        fileName="debug.txt"
        layout="[${longdate:universalTime=true} ${threadid}${mdlc:item=id}] ${level:uppercase=true}: ${callsite} ${message}"
        encoding="utf-8"
        archiveNumbering="DateAndSequence"
        archiveAboveSize="250000000"/>
    <target xsi:type="null" name="null" formatMessage="false" />
  </targets>

  <rules>
    <!-- Avoid logging to incorrect folder before the logging initialization is done. If you want to see those logging messages, comment out this line, but your log file will be somewhere else. -->
    <logger name="*" minlevel="Trace" writeTo="null" final="true" />

    <!-- Log Consensus related entries -->
    <logger name="Stratis.Bitcoin.Features.Consensus.*" minlevel="Trace" writeTo="debugFile" />
    <logger name="Stratis.Bitcoin.Consensus.*" minlevel="Trace" writeTo="debugFile" />
  </rules>
</nlog>
```