using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DistributedEventSystem
{
    public class Event
    {
        public string EventType { get; }
        public string Data { get; }
        public int Timestamp { get; }
        public int NodeId { get; }

        public Event(string eventType, string data, int timestamp, int nodeId)
        {
            EventType = eventType;
            Data = data;
            Timestamp = timestamp;
            NodeId = nodeId;
        }

        public override string ToString()
        {
            return $"[Event: {EventType}, Data: {Data}, Timestamp: {Timestamp}, Node: {NodeId}]";
        }
    }

    public class Node
    {
        public int NodeId { get; }
        private int _lamportClock;
        private readonly EventManager _eventManager;

        public Node(int nodeId, EventManager eventManager)
        {
            NodeId = nodeId;
            _lamportClock = 0;
            _eventManager = eventManager;
        }

        public void PublishEvent(string eventType, string data)
        {
            _lamportClock++;
            var newEvent = new Event(eventType, data, _lamportClock, NodeId);
            _eventManager.BroadcastEvent(newEvent);
        }

        public void ReceiveEvent(Event ev)
        {
            _lamportClock = Math.Max(_lamportClock, ev.Timestamp) + 1;
            Console.WriteLine($"Node {NodeId} received: {ev}");
        }

        public void Subscribe(string eventType)
        {
            _eventManager.Subscribe(eventType, this);
        }

        public void Unsubscribe(string eventType)
        {
            _eventManager.Unsubscribe(eventType, this);
        }
    }

    public class EventManager
    {
        private readonly ConcurrentDictionary<string, List<Node>> _subscriptions;
        private readonly object _lock = new object();

        public EventManager()
        {
            _subscriptions = new ConcurrentDictionary<string, List<Node>>();
        }

        public void Subscribe(string eventType, Node node)
        {
            lock (_lock)
            {
                if (!_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType] = new List<Node>();
                }
                _subscriptions[eventType].Add(node);
            }
        }

        public void Unsubscribe(string eventType, Node node)
        {
            lock (_lock)
            {
                if (_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType].Remove(node);
                    if (_subscriptions[eventType].Count == 0)
                    {
                        _subscriptions.TryRemove(eventType, out _);
                    }
                }
            }
        }

        public void BroadcastEvent(Event ev)
        {
            if (_subscriptions.TryGetValue(ev.EventType, out var subscribers))
            {
                foreach (var subscriber in subscribers)
                {
                    ThreadPool.QueueUserWorkItem(_ => subscriber.ReceiveEvent(ev));
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var eventManager = new EventManager();

            var node1 = new Node(1, eventManager);
            var node2 = new Node(2, eventManager);
            var node3 = new Node(3, eventManager);

            node1.Subscribe("TypeA");
            node2.Subscribe("TypeB");
            node3.Subscribe("TypeC");

            node1.PublishEvent("TypeA", "Hello from Node 1");
            node2.PublishEvent("TypeB", "Hello from Node 2");
            node3.PublishEvent("TypeC", "Hello from Node 3");

            var node4 = new Node(4, eventManager);
            node4.Subscribe("TypeA");
            node4.PublishEvent("TypeA", "Hello from Node 4");

            node1.Unsubscribe("TypeC");

            Console.ReadLine();
        }
    }
}
