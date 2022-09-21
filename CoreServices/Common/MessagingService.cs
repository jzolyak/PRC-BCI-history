using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreServices.Common
{
    public class MessagingService
    {
        private static ConcurrentDictionary<MessageType,List<Subscriber>> subscriptionDictionary = new ConcurrentDictionary<MessageType, List<Subscriber>>();

        public static void Subscribe(MessageType messageType,Subscriber subscriber)
        {
            if(!subscriptionDictionary.ContainsKey(messageType))
            {
                subscriptionDictionary.TryAdd(messageType, new List<Subscriber>());
            }
            List<Subscriber> subscribers;
            while(subscriptionDictionary.TryGetValue(messageType, out subscribers))
            {
                subscribers.Add(subscriber);
            }

        }
        public static void SendMessage(Object sender, string msg, MessageType messageType)
        {
            if (!subscriptionDictionary.ContainsKey(messageType)) return;
            List<Subscriber> subscribers;
            if (subscriptionDictionary.TryGetValue(messageType, out subscribers))
            {
                if (subscribers.Count == 0) return;
                subscribers.ForEach(subscriber => { if (subscriber.MessageReceiver != null && subscriber.MessageReceiver.IsAlive && subscriber.Action != null) subscriber.Action(new MessageArgs { MessageType = messageType, Msg = msg, Sender = sender }); });
            }
        }
    }
    public class MessageArgs
    {
        public Object Sender { get; set; }
        public string Msg { get; set; }
        public MessageType MessageType { get; set; }
    }
    public enum MessageType
    {
        Popup,
        UI
    }

    public interface IMessageReceiver
    {
        public bool IsAlive { get; set; }
    }

    public class Subscriber
    {
        public IMessageReceiver MessageReceiver { get; set; }
        public Action<MessageArgs> Action { get; set; }
    }

}
