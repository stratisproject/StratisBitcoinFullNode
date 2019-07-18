using System;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;

namespace Stratis.Bitcoin.Features.SignalR.Events
{
    public class TransactionReceivedClientEvent : IClientEvent
    {
        public string TxHash { get; set; }

        public bool IsCoinbase { get; set; }

        public bool IsCoinstake { get; set; }

        public uint Time { get; set; }

        public Type NodeEventType { get; } = typeof(TransactionReceived);

        public void BuildFrom(EventBase @event)
        {
            if (@event is TransactionReceived transactionReceived)
            {
                this.TxHash = transactionReceived.ReceivedTransaction.GetHash().ToString();
                this.IsCoinbase = transactionReceived.ReceivedTransaction.IsCoinBase;
                this.IsCoinstake = transactionReceived.ReceivedTransaction.IsCoinStake;
                this.Time = transactionReceived.ReceivedTransaction.Time;
                return;
            }

            throw new ArgumentException();
        }
    }
}