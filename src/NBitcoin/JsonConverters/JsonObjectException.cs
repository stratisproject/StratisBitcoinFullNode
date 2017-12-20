#if !NOJSONNET
using System;
using Newtonsoft.Json;

namespace NBitcoin.JsonConverters
{
#if !NOJSONNET
    public
#else
    internal
#endif
    class JsonObjectException : Exception
    {
        public JsonObjectException(Exception inner, JsonReader reader)
            : base(inner.Message, inner)
        {
            Path = reader.Path;
        }
        public JsonObjectException(string message, JsonReader reader)
            : base(message)
        {
            Path = reader.Path;
        }

        public string Path
        {
            get;
            private set;
        }
    }
}
#endif