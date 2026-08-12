using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking.Messaging
{
    using NetworkManager = Xoderony.Networking.NetworkManager;
    using BufferWriter = Xoderony.Networking.BufferWriter;
    using BufferReader = Xoderony.Networking.BufferReader;

    public delegate void NetworkMessageHandler(ulong senderClientId, BufferReader reader);

    /// <summary>
    /// Typed messages with Host relay for Distributed Authority topology.
    /// Wire format: ushort type | ulong senderClientId | payload...
    /// </summary>
    public sealed class CustomMessagingManager
    {
        private readonly NetworkManager _networkManager;
        private readonly Dictionary<ushort, NetworkMessageHandler> _handlers = new Dictionary<ushort, NetworkMessageHandler>();
        private readonly BufferWriter _writeBuffer = new BufferWriter(512);
        private readonly BufferReader _readBuffer = new BufferReader(512);
        private readonly BufferReader _handlerReader = new BufferReader(512);

        internal CustomMessagingManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public void Register(ushort messageType, NetworkMessageHandler handler)
        {
            if (_handlers.TryGetValue(messageType, out var existing))
            {
                _handlers[messageType] = existing + handler;
            }
            else
            {
                _handlers[messageType] = handler;
            }
        }

        public void Unregister(ushort messageType, NetworkMessageHandler handler)
        {
            if (!_handlers.TryGetValue(messageType, out var existing))
            {
                return;
            }

            existing -= handler;
            if (existing == null)
            {
                _handlers.Remove(messageType);
            }
            else
            {
                _handlers[messageType] = existing;
            }
        }

        /// <summary>
        /// Send to all remote peers (via Host relay when caller is a client).
        /// </summary>
        public void SendToOthers(ushort messageType, BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            if (!_networkManager.IsConnected)
            {
                throw new InvalidOperationException("Session is not connected.");
            }

            BuildEnvelope(messageType, _networkManager.LocalClientId, payload.AsSegment());
            var segment = _writeBuffer.AsSegment();

            if (_networkManager.IsHost)
            {
                _networkManager.BroadcastRaw(segment, delivery, excludeTransportId: 0);
            }
            else
            {
                _networkManager.SendRawToServer(segment, delivery);
            }
        }

        /// <summary>
        /// Host-only: send envelope to one transport peer.
        /// </summary>
        internal void SendRawToTransportPeer(
            ulong transportPeerId,
            ushort messageType,
            ulong senderClientId,
            ArraySegment<byte> payload,
            NetworkDelivery delivery)
        {
            BuildEnvelope(messageType, senderClientId, payload);
            _networkManager.SendRaw(transportPeerId, _writeBuffer.AsSegment(), delivery);
        }

        internal void HandleIncoming(ulong transportPeerId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            _readBuffer.Load(data);
            var messageType = _readBuffer.ReadUShort();
            var senderClientId = _readBuffer.ReadULong();
            var payloadLength = _readBuffer.Length - _readBuffer.Position;
            var payload = _readBuffer.ReadByteSegment(payloadLength);

            if (_handlers.TryGetValue(messageType, out var handler))
            {
                _handlerReader.Load(payload);
                handler.Invoke(senderClientId, _handlerReader);
            }

            // Host relays after local handling. Spawn requests (networkObjectId == 0) are host-consumed
            // and replaced by an authoritative Spawn; do not forward the request payload.
            if (_networkManager.IsHost &&
                transportPeerId != _networkManager.NetworkTransport.LocalTransportId &&
                ShouldRelay(messageType, payload))
            {
                _networkManager.BroadcastRaw(data, delivery, excludeTransportId: transportPeerId);
            }
        }

        private static bool ShouldRelay(ushort messageType, ArraySegment<byte> payload)
        {
            if (messageType == NetworkMessageType.Welcome)
            {
                return false;
            }

            if (messageType == NetworkMessageType.Spawn && payload.Count >= 4)
            {
                var networkObjectId = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan());
                if (networkObjectId == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildEnvelope(ushort messageType, ulong senderClientId, ArraySegment<byte> payload)
        {
            _writeBuffer.Clear();
            _writeBuffer.WriteUShort(messageType);
            _writeBuffer.WriteULong(senderClientId);
            if (payload.Count > 0)
            {
                _writeBuffer.WriteBytes(payload);
            }
        }
    }
}
