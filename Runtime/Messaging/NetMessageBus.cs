using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Xoderony.Networking
{
    public delegate void NetMessageHandler(ulong senderClientId, NetBuffer reader);

    /// <summary>
    /// Typed messages with Host relay for Distributed Authority topology.
    /// Wire format: ushort type | ulong senderClientId | payload...
    /// </summary>
    public sealed class NetMessageBus
    {
        private readonly NetSession _session;
        private readonly Dictionary<ushort, NetMessageHandler> _handlers = new Dictionary<ushort, NetMessageHandler>();
        private readonly NetBuffer _writeBuffer = new NetBuffer(512);
        private readonly NetBuffer _readBuffer = new NetBuffer(512);

        internal NetMessageBus(NetSession session)
        {
            _session = session;
        }

        public void Register(ushort messageType, NetMessageHandler handler)
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

        public void Unregister(ushort messageType, NetMessageHandler handler)
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
        public void SendToOthers(ushort messageType, NetBuffer payload, NetDelivery delivery = NetDelivery.Reliable)
        {
            if (!_session.IsConnected)
            {
                throw new InvalidOperationException("Session is not connected.");
            }

            BuildEnvelope(messageType, _session.LocalClientId, payload.AsSegment());
            var segment = _writeBuffer.AsSegment();

            if (_session.IsHost)
            {
                _session.BroadcastRaw(segment, delivery, excludeTransportId: 0);
            }
            else
            {
                _session.SendRawToHost(segment, delivery);
            }
        }

        /// <summary>
        /// Host-only: send envelope to one transport peer.
        /// </summary>
        internal void SendRawToTransportPeer(ulong transportPeerId, ushort messageType, ulong senderClientId, ArraySegment<byte> payload, NetDelivery delivery)
        {
            BuildEnvelope(messageType, senderClientId, payload);
            _session.SendRaw(transportPeerId, _writeBuffer.AsSegment(), delivery);
        }

        internal void HandleIncoming(ulong transportPeerId, ArraySegment<byte> data, NetDelivery delivery)
        {
            _readBuffer.Load(data);
            var messageType = _readBuffer.ReadUShort();
            var senderClientId = _readBuffer.ReadULong();
            var payloadOffset = _readBuffer.Position;
            var payloadLength = _readBuffer.Length - payloadOffset;
            var payload = _readBuffer.ReadByteSegment(payloadLength);

            if (_handlers.TryGetValue(messageType, out var handler))
            {
                var reader = new NetBuffer();
                reader.Load(payload);
                handler.Invoke(senderClientId, reader);
            }

            // Host relays after local handling. Spawn requests (networkId == 0) are host-consumed
            // and replaced by an authoritative Spawn; do not forward the request payload.
            if (_session.IsHost &&
                transportPeerId != _session.Transport.LocalTransportId &&
                ShouldRelay(messageType, payload))
            {
                _session.BroadcastRaw(data, delivery, excludeTransportId: transportPeerId);
            }
        }

        private static bool ShouldRelay(ushort messageType, ArraySegment<byte> payload)
        {
            if (messageType == NetMessageType.Welcome)
            {
                return false;
            }

            if (messageType == NetMessageType.Spawn && payload.Count >= 4)
            {
                var networkId = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan());
                if (networkId == 0)
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
