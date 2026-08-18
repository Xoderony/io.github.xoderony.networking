using System;
using System.Collections.Generic;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>
    /// 对等消息实现：直接订阅传输事件，以 byte 类型路由消息并向已连接对端网格直发。
    /// 构造后须在 Transport 启动前保持存活，Transport 停止后由拥有方调用 <see cref="Dispose"/>。
    /// </summary>
    public sealed class NetworkMessageManager : INetworkMessageManager, IDisposable {
        private readonly INetworkTransport _transport;
        private readonly HashSet<ulong> _peers = new HashSet<ulong>();
        // 消息类型为 byte（0–255），数组下标映射 O(1)。
        private readonly NetworkMessageHandler[] _handlers = new NetworkMessageHandler[byte.MaxValue + 1];

        public NetworkMessageManager(INetworkTransport transport) {
            _transport = transport;
            transport.PeerConnected += OnPeerConnected;
            transport.PeerDisconnected += OnPeerDisconnected;
            transport.DataReceived += OnDataReceived;
        }

        public void RegisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] += handler;
        }

        public void UnregisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] -= handler;
        }

        public void SendToOthers(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            foreach (var peerId in _peers) {
                _transport.SendData(peerId, message, delivery);
            }
        }

        /// <summary>
        /// 先发对端再本地回显：保证本条消息先于本地 handler 派生的后续消息到达对端（Reliable 有序下）。
        /// </summary>
        public void SendToAll(ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            SendToOthers(message, delivery);
            var reader = new BufferReader(message);
            var messageType = reader.ReadByte();
            _handlers[messageType]?.Invoke(_transport.LocalPeerId, reader);
        }

        public void SendToPeer(ulong peerId, ReadOnlySpan<byte> message, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            _transport.SendData(peerId, message, delivery);
        }

        public void Dispose() {
            _transport.PeerConnected -= OnPeerConnected;
            _transport.PeerDisconnected -= OnPeerDisconnected;
            _transport.DataReceived -= OnDataReceived;
            _peers.Clear();
            Array.Clear(_handlers, 0, _handlers.Length);
        }

        private void OnPeerConnected(ulong peerId) {
            _peers.Add(peerId);
        }

        private void OnPeerDisconnected(ulong peerId) {
            _peers.Remove(peerId);
        }

        private void OnDataReceived(ulong peerId, ReadOnlySpan<byte> data, NetworkDelivery delivery) {
            var reader = new BufferReader(data);
            var messageType = reader.ReadByte();
            _handlers[messageType]?.Invoke(peerId, reader);
        }
    }
}
