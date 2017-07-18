using System;
using System.Reactive.Subjects;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalerTest
    {
        public SignalerTest()
        {            
        }

        /// <remarks>
        /// Because of the AsObservable that wraps classes in internal reactive classes it's hard to prove that the observer provided is subscribed to the subject so 
        /// we prove this by calling the onnext of the observer to prove it's the one we provided.
        /// </remarks>
        [Fact]
        public void SubscribeRegistersObserverWithObservable()
        {
            var block = new Block();
            var subject = new Mock<ISubject<Block>>();            
            var observer = new Mock<IObserver<Block>>();            
            subject.Setup(s => s.Subscribe(It.IsAny<IObserver<Block>>()))
                .Callback<IObserver<Block>>((o) =>
                {
                    o.OnNext(block);                    
                });

            var signaler = new Signaler<Block>(subject.Object);

            var result = signaler.Subscribe(observer.Object);

            observer.Verify(v => v.OnNext(block), Times.Exactly(1));
        }

        [Fact]
        public void BroadcastSignalsSubject()
        {
            var block = new Block();
            var subject = new Mock<ISubject<Block>>();            
            var signaler = new Signaler<Block>(subject.Object);

            signaler.Broadcast(block);

            subject.Verify(s => s.OnNext(block), Times.Exactly(1));
        }
    }
}