using System;
using System.Diagnostics;

namespace NBitcoin.Protocol
{
    public static class NodeServerTrace
    {
        private static TraceSource trace = new TraceSource("NBitcoin.NodeServer");
        public static TraceSource Trace { get { return trace; } }

        public static void Transfer(Guid activityId)
        {
        }

        public static void ErrorWhileRetrievingDNSSeedIp(string name, Exception ex)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, "Impossible to resolve dns for seed " + name + " " + Utils.ExceptionToString(ex));
        }

        public static void Warning(string msg, Exception ex)
        {
            trace.TraceEvent(TraceEventType.Warning, 0, msg + " " + Utils.ExceptionToString(ex));
        }

        public static void ExternalIpReceived(string ip)
        {
            trace.TraceInformation("External ip received : " + ip);
        }

        internal static void ExternalIpFailed(Exception ex)
        {
            trace.TraceEvent(TraceEventType.Error, 0, "External ip cannot be detected " + Utils.ExceptionToString(ex));
        }

        public static void Information(string info)
        {
            trace.TraceInformation(info);
        }

        public static void Error(string msg, Exception ex)
        {
            trace.TraceEvent(TraceEventType.Error, 0, msg + " " + Utils.ExceptionToString(ex));
        }

        public static void Warning(string msg)
        {
            Warning(msg, null);
        }

        public static void PeerTableRemainingPeerToGet(int count)
        {
            trace.TraceInformation("Remaining peer to get : " + count);
        }

        public static void ConnectionToSelfDetected()
        {
            Warning("Connection to self detected, abort connection");
        }

        public static void Verbose(string str)
        {
            trace.TraceEvent(TraceEventType.Verbose, 0, str);
        }
    }
}
