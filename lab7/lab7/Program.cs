using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DistributedSystem
{
    public enum NodeStatus
    {
        Active,
        Inactive
    }

    public class DistributedSystemNode
    {
        private readonly string _nodeId;
        private NodeStatus _status;
        private readonly List<DistributedSystemNode> _connectedNodes;
        private readonly BlockingCollection<string> _messageQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public string NodeId => _nodeId;

        public NodeStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyStatusChangeAsync().ConfigureAwait(false);
                }
            }
        }

        public DistributedSystemNode(string nodeId)
        {
            _nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _status = NodeStatus.Inactive;
            _connectedNodes = new List<DistributedSystemNode>();
            _messageQueue = new BlockingCollection<string>();
            _cancellationTokenSource = new CancellationTokenSource();
            StartMessageProcessing();
        }

        public void ConnectNode(DistributedSystemNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _connectedNodes.Add(node);
        }

        public async Task SendMessageAsync(string message, DistributedSystemNode recipient)
        {
            if (recipient == null) throw new ArgumentNullException(nameof(recipient));
            Console.WriteLine($"{_nodeId} is sending message to {recipient.NodeId}: {message}");
            await Task.Run(() => recipient.ReceiveMessage(message));
        }

        private void ReceiveMessage(string message)
        {
            _messageQueue.Add(message);
        }

        private async void StartMessageProcessing()
        {
            await Task.Run(() =>
            {
                foreach (var message in _messageQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                {
                    ProcessMessage(message).ConfigureAwait(false);
                }
            });
        }

        private async Task ProcessMessage(string message)
        {
            await Task.Delay(100);
            Console.WriteLine($"{_nodeId} processed message: {message}");
        }

        private async Task NotifyStatusChangeAsync()
        {
            foreach (var node in _connectedNodes)
            {
                await SendMessageAsync($"Status of {_nodeId} changed to {_status}", node);
            }
        }

        public async Task ActivateAsync()
        {
            Console.WriteLine($"{_nodeId} is activating...");
            Status = NodeStatus.Active;
            await Task.Delay(100);
            Console.WriteLine($"{_nodeId} is now active.");
        }

        public async Task DeactivateAsync()
        {
            Console.WriteLine($"{_nodeId} is deactivating...");
            Status = NodeStatus.Inactive;
            await Task.Delay(100);
            Console.WriteLine($"{_nodeId} is now inactive.");
        }

        public void StopProcessing()
        {
            _cancellationTokenSource.Cancel();
            _messageQueue.CompleteAdding();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var nodeA = new DistributedSystemNode("uzelA");
            var nodeB = new DistributedSystemNode("uzelB");
            var nodeC = new DistributedSystemNode("uzelC");

            nodeA.ConnectNode(nodeB);
            nodeA.ConnectNode(nodeC);

            await nodeA.ActivateAsync();
            await nodeA.SendMessageAsync("Hello, uzelB!", nodeB);

            await nodeA.DeactivateAsync();

            nodeA.StopProcessing();
            nodeB.StopProcessing();
            nodeC.StopProcessing();
        }
    }
}