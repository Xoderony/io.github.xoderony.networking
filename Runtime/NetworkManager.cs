using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>
    /// 对等会话实现：构造注入传输、启动/停止、消息协议与网格直发。
    /// 无服务器/客户端之分：所有对端平等，PeerId 即传输端 id（Steam 下为 SteamID）。
    /// 不依赖 MonoBehaviour：每帧由外部驱动调用 <see cref="Poll"/>，清理由调用方调用 <see cref="Stop"/>。
    /// </summary>
    public class NetworkManager : INetworkManager {
        /// <summary>信封缓冲：type(1) + sender(8) + 载荷上限；固定容量，不动态扩容。</summary>
        private const int EnvelopeCapacity = sizeof(byte) + sizeof(ulong) + NetworkMessageLimits.PayloadCapacity;

        private readonly INetworkTransport _transport;
        private readonly HashSet<ulong> _peers = new HashSet<ulong>();
        // 消息类型为 byte（0–255），数组下标映射 O(1)。
        private readonly NetworkMessageHandler[] _handlers = new NetworkMessageHandler[byte.MaxValue + 1];
        private readonly byte[] _envelopeBuffer = new byte[EnvelopeCapacity];

        public INetworkTransport NetworkTransport => _transport;
        public SessionState State { get; private set; } = SessionState.Stopped;
        public ulong LocalPeerId { get; private set; }
        public bool IsRunning => State == SessionState.Running;

        public event Action Started;
        public event Action Stopped;
        public event Action<ulong> PeerJoined;
        public event Action<ulong> PeerLeft;

        public NetworkManager(INetworkTransport transport) {
            _transport = transport;
        }

        public bool Start() {
            if (State != SessionState.Stopped) {
                return false;
            }

            SubscribeTransportEvents();
            if (!_transport.Start()) {
                UnsubscribeTransportEvents();
                return false;
            }

            LocalPeerId = _transport.LocalPeerId;
            State = SessionState.Running;
            Started?.Invoke();
            return true;
        }

        public void Stop() {
            if (State == SessionState.Stopped) {
                return;
            }

            UnsubscribeTransportEvents();
            _transport.Stop();
            _peers.Clear();
            LocalPeerId = 0;
            State = SessionState.Stopped;
            Stopped?.Invoke();
        }

        public void Poll() {
            _transport.Poll();
        }

        public void RegisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] += handler;
        }

        public void UnregisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] -= handler;
        }

        public void SendToOthers(byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            var envelope = BuildEnvelope(messageType, LocalPeerId, payload);
            foreach (var peerId in _peers) {
                _transport.SendData(peerId, envelope, delivery);
            }
        }

        /// <summary>
        /// 先发对端再本地回显：保证本条消息先于本地 handler 派生的后续消息到达对端（Reliable 有序下）。
        /// </summary>
        public void SendToAll(byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            SendToOthers(messageType, payload, delivery);
            _handlers[messageType]?.Invoke(LocalPeerId, new BufferReader(payload));
        }

        public void SendToPeer(ulong peerId, byte messageType, ReadOnlySpan<byte> payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            var envelope = BuildEnvelope(messageType, LocalPeerId, payload);
            _transport.SendData(peerId, envelope, delivery);
        }

        public bool ConnectPeer(ulong peerId) {
            return _transport.ConnectPeer(peerId);
        }

        private void SubscribeTransportEvents() {
            _transport.PeerConnected += OnTransportPeerConnected;
            _transport.PeerDisconnected += OnTransportPeerDisconnected;
            _transport.DataReceived += OnTransportDataReceived;
        }

        private void UnsubscribeTransportEvents() {
            _transport.PeerConnected -= OnTransportPeerConnected;
            _transport.PeerDisconnected -= OnTransportPeerDisconnected;
            _transport.DataReceived -= OnTransportDataReceived;
        }

        private void OnTransportPeerConnected(ulong transportPeerId) {
            if (_peers.Add(transportPeerId)) {
                PeerJoined?.Invoke(transportPeerId);
            }
        }

        private void OnTransportPeerDisconnected(ulong transportPeerId) {
            if (_peers.Remove(transportPeerId)) {
                PeerLeft?.Invoke(transportPeerId);
            }
        }

        private void OnTransportDataReceived(ulong transportPeerId, ReadOnlySpan<byte> data, NetworkDelivery delivery) {
            var reader = new BufferReader(data);
            var messageType = reader.ReadByte();
            var senderPeerId = reader.ReadULong();
            var payload = reader.Buffer[reader.Position..];

            var handler = _handlers[messageType];
            handler?.Invoke(senderPeerId, new BufferReader(payload));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> BuildEnvelope(byte messageType, ulong senderPeerId, ReadOnlySpan<byte> payload) {
            var writer = new BufferWriter(_envelopeBuffer);
            writer.WriteByte(messageType);
            writer.WriteULong(senderPeerId);
            writer.WriteBytes(payload);
            return writer.Written;
        }
    }
}
