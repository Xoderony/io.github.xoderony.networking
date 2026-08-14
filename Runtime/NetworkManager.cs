using System;
using System.Collections.Generic;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>网络消息处理委托。</summary>
    public delegate void NetworkMessageHandler(ulong senderPeerId, BufferReader reader);

    /// <summary>
    /// 对等会话实现：构造注入传输、启动/停止、消息协议与网格直发。
    /// 无服务器/客户端之分：所有对端平等，PeerId 即传输端 id（Steam 下为 SteamID）。
    /// 不依赖 MonoBehaviour：每帧由外部驱动调用 <see cref="Poll"/>，清理由调用方调用 <see cref="Stop"/>。
    /// 生成管理器（SpawnManager）由生成模块接入。
    /// </summary>
    public class NetworkManager : INetworkManager {
        private readonly INetworkTransport _transport;
        private readonly HashSet<ulong> _peers = new HashSet<ulong>();
        // 消息类型为 byte（0–255），数组下标映射 O(1)。
        private readonly NetworkMessageHandler[] _handlers = new NetworkMessageHandler[byte.MaxValue + 1];
        /// <summary>内置协议最大固定头：Spawn 的 LocalId + PrefabId + 位置/旋转（取安全上限，不感知具体协议）。</summary>
        private const int MaxProtocolHeaderSize = sizeof(uint) + sizeof(ushort) + (sizeof(float) * 7);

        /// <summary>信封缓冲容量：type(1) + sender(8) + 最大固定头 + 状态数据上限；固定容量，不动态扩容。</summary>
        private const int EnvelopeCapacity = sizeof(byte) + sizeof(ulong) + MaxProtocolHeaderSize + NetworkMessageLimits.StateDataCapacity;

        private readonly byte[] _envelopeBuffer = new byte[EnvelopeCapacity];
        private int _envelopeLength;

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

        /// <summary>每帧驱动传输处理底层事件；由外部生命周期调用。</summary>
        public void Poll() {
            _transport.Poll();
        }

        /// <summary>注册消息处理。</summary>
        public void RegisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] += handler;
        }

        /// <summary>注销消息处理。</summary>
        public void UnregisterMessage(byte messageType, NetworkMessageHandler handler) {
            _handlers[messageType] -= handler;
        }

        /// <summary>发送给所有已连接对端（网格直发）。</summary>
        public void SendToOthers(byte messageType, BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            BuildEnvelope(messageType, LocalPeerId, payload.Written);
            foreach (var peerId in _peers) {
                _transport.SendData(peerId, Envelope, delivery);
            }
        }

        /// <summary>
        /// 广播给所有已连接对端并本地投递（本地回显走同一 handler）。
        /// 先发对端再本地回显：保证本条消息先于本地 handler 派生的后续消息到达对端（Reliable 有序下）。
        /// </summary>
        public void SendToAll(byte messageType, BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            SendToOthers(messageType, payload, delivery);
            _handlers[messageType]?.Invoke(LocalPeerId, new BufferReader(payload.Written));
        }

        /// <summary>定向发送给指定对端（需已建立连接）。</summary>
        public void SendToPeer(ulong peerId, byte messageType, BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable) {
            BuildEnvelope(messageType, LocalPeerId, payload.Written);
            _transport.SendData(peerId, Envelope, delivery);
        }

        /// <summary>建立到指定对端（SteamID）的直连。</summary>
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

        private void BuildEnvelope(byte messageType, ulong senderPeerId, ReadOnlySpan<byte> payload) {
            var writer = new BufferWriter(_envelopeBuffer);
            writer.WriteByte(messageType);
            writer.WriteULong(senderPeerId);
            writer.WriteBytes(payload);
            _envelopeLength = writer.DataLength;
        }

        private ReadOnlySpan<byte> Envelope => _envelopeBuffer.AsSpan(0, _envelopeLength);
    }
}
