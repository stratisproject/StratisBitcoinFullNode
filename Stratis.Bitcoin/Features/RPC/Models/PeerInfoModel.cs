using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.RPC.Models
{
    public class PeerInfoModel
    {
        public int id
        {
            get; internal set;
        }

        public string addr
        {
            get; internal set;
        }
        public string addrlocal
        {
            get; internal set;
        }
        public string services
        {
            get; internal set;
        }
        public int lastsend
        {
            get; internal set;
        }
        public int lastrecv
        {
            get; internal set;
        }
        public long bytessent
        {
            get; internal set;
        }
        public long bytesrecv
        {
            get; internal set;
        }
        public int conntime
        {
            get; internal set;
        }
        public int timeoffset
        {
            get; internal set;
        }
        public double pingtime
        {
            get; internal set;
        }
        public double minping
        {
            get; internal set;
        }
        public double pingwait
        {
            get; internal set;
        }
        public int version
        {
            get; internal set;
        }
        public string subver
        {
            get; internal set;
        }
        public bool inbound
        {
            get; internal set;
        }
        public int startingheight
        {
            get; internal set;
        }
        public int banscore
        {
            get; internal set;
        }
        public int synced_headers
        {
            get; internal set;
        }
        public int synced_blocks
        {
            get; internal set;
        }

        public uint[] inflight
        {
            get; internal set;
        }
        public bool whitelisted
        {
            get; internal set;
        }
        //todo: bytessent_per_msg, bytesrecv_per_msg
    }
}
