using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace NBitcoin
{
    public class ConsensusFactory
    {
        private readonly ConcurrentDictionary<Type, bool> isAssignableFromBlockHeader = new ConcurrentDictionary<Type, bool>();
        private readonly TypeInfo blockHeaderType = typeof(BlockHeader).GetTypeInfo();

        private readonly ConcurrentDictionary<Type, bool> isAssignableFromBlock = new ConcurrentDictionary<Type, bool>();
        private readonly TypeInfo blockType = typeof(Block).GetTypeInfo();

        private readonly ConcurrentDictionary<Type, bool> isAssignableFromTransaction = new ConcurrentDictionary<Type, bool>();
        private readonly TypeInfo transactionType = typeof(Transaction).GetTypeInfo();

        protected bool IsBlockHeader<T>()
        {
            return this.IsAssignable<T>(this.blockHeaderType, this.isAssignableFromBlockHeader);
        }

        protected bool IsBlock<T>()
        {
            return this.IsAssignable<T>(this.blockType, this.isAssignableFromBlock);
        }

        protected bool IsTransaction<T>()
        {
            return this.IsAssignable<T>(this.transactionType, this.isAssignableFromTransaction);
        }

        private bool IsAssignable<T>(TypeInfo type, ConcurrentDictionary<Type, bool> cache)
        {
            bool isAssignable = false;
            if (!cache.TryGetValue(typeof(T), out isAssignable))
            {
                isAssignable = type.IsAssignableFrom(typeof(T).GetTypeInfo());
                cache.TryAdd(typeof(T), isAssignable);
            }
            return isAssignable;
        }

        public virtual bool TryCreateNew<T>(out T result) where T : IBitcoinSerializable
        {
            result = default(T);
            if (IsBlock<T>())
            {
                result = (T)(object)CreateBlock();
                return true;
            }
            if (IsBlockHeader<T>())
            {
                result = (T)(object)CreateBlockHeader();
                return true;
            }
            if (IsTransaction<T>())
            {
                result = (T)(object)CreateTransaction();
                return true;
            }
            return false;
        }

        public virtual ProtocolCapabilities GetProtocolCapabilities(uint protocolVersion)
        {
            return new ProtocolCapabilities()
            {
                PeerTooOld = protocolVersion < 209U,
                SupportTimeAddress = protocolVersion >= 31402U,
                SupportGetBlock = protocolVersion < 32000U || protocolVersion > 32400U,
                SupportPingPong = protocolVersion > 60000U,
                SupportMempoolQuery = protocolVersion >= 60002U,
                SupportReject = protocolVersion >= 70002U,
                SupportNodeBloom = protocolVersion >= 70011U,
                SupportSendHeaders = protocolVersion >= 70012U,
                SupportWitness = protocolVersion >= 70012U,
                SupportCompactBlocks = protocolVersion >= 70014U,
                SupportCheckSum = protocolVersion >= 60002,
                SupportUserAgent = protocolVersion >= 60002
            };
        }

        public virtual Block CreateBlock()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new Block(this.CreateBlockHeader());
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual BlockHeader CreateBlockHeader()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new BlockHeader();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual Transaction CreateTransaction()
        {
            return new Transaction();
        }
    }

    public class ProtocolCapabilities
    {
        /// <summary>
        /// Disconnect from peers older than this protocol version
        /// </summary>
        public bool PeerTooOld
        {
            get; set;
        }

        /// <summary>
        /// nTime field added to CAddress, starting with this version;
        /// if possible, avoid requesting addresses nodes older than this
        /// </summary>
        public bool SupportTimeAddress
        {
            get; set;
        }

        public bool SupportGetBlock
        {
            get; set;
        }
        /// <summary>
        /// BIP 0031, pong message, is enabled for all versions AFTER this one
        /// </summary>
        public bool SupportPingPong
        {
            get; set;
        }

        /// <summary>
        /// "mempool" command, enhanced "getdata" behavior starts with this version
        /// </summary>
        public bool SupportMempoolQuery
        {
            get; set;
        }

        /// <summary>
        /// "reject" command
        /// </summary>
        public bool SupportReject
        {
            get; set;
        }

        /// <summary>
        /// ! "filter*" commands are disabled without NODE_BLOOM after and including this version
        /// </summary>
        public bool SupportNodeBloom
        {
            get; set;
        }

        /// <summary>
        /// ! "sendheaders" command and announcing blocks with headers starts with this version
        /// </summary>
        public bool SupportSendHeaders
        {
            get; set;
        }

        /// <summary>
        /// ! Version after which witness support potentially exists
        /// </summary>
        public bool SupportWitness
        {
            get; set;
        }

        /// <summary>
        /// short-id-based block download starts with this version
        /// </summary>
        public bool SupportCompactBlocks
        {
            get; set;
        }

        /// <summary>
        /// Support checksum at p2p message level
        /// </summary>
        public bool SupportCheckSum
        {
            get;
            set;
        }
        public bool SupportUserAgent
        {
            get;
            set;
        }

        public static ProtocolCapabilities CreateSupportAll()
        {
            return new ProtocolCapabilities()
            {
                PeerTooOld = false,
                SupportCheckSum = true,
                SupportCompactBlocks = true,
                SupportGetBlock = true,
                SupportMempoolQuery = true,
                SupportNodeBloom = true,
                SupportPingPong = true,
                SupportReject = true,
                SupportSendHeaders = true,
                SupportTimeAddress = true,
                SupportUserAgent = true,
                SupportWitness = true
            };
        }

        public bool IsSupersetOf(ProtocolCapabilities capabilities)
        {
            return (!capabilities.SupportCheckSum || SupportCheckSum) &&
                (!capabilities.SupportCompactBlocks || SupportCompactBlocks) &&
                (!capabilities.SupportGetBlock || SupportGetBlock) &&
                (!capabilities.SupportMempoolQuery || SupportMempoolQuery) &&
                (!capabilities.SupportNodeBloom || SupportNodeBloom) &&
                (!capabilities.SupportPingPong || SupportPingPong) &&
                (!capabilities.SupportReject || SupportReject) &&
                (!capabilities.SupportSendHeaders || SupportSendHeaders) &&
                (!capabilities.SupportTimeAddress || SupportTimeAddress) &&
                (!capabilities.SupportWitness || SupportWitness) &&
                (!capabilities.SupportUserAgent || SupportUserAgent) &&
                (!capabilities.SupportCheckSum || SupportCheckSum);
        }
    }

}
