using System;
using UnityEngine;

namespace Xoderony.Networking
{
    /// <summary>
    /// Network identity on a GameObject. Owner may push <see cref="SendState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkEntity : MonoBehaviour
    {
        private NetSession _session;

        public uint NetworkId { get; internal set; }
        public ulong OwnerClientId { get; internal set; }
        public bool IsSpawned { get; internal set; }
        public ushort PrefabId { get; internal set; }

        public NetSession Session => _session;
        public bool IsOwner => IsSpawned && _session != null && _session.LocalClientId == OwnerClientId;

        internal void Bind(NetSession session, uint networkId, ulong ownerClientId, ushort prefabId)
        {
            _session = session;
            NetworkId = networkId;
            OwnerClientId = ownerClientId;
            PrefabId = prefabId;
            IsSpawned = true;
            OnNetworkSpawn();
        }

        internal void Unbind()
        {
            if (!IsSpawned)
            {
                return;
            }

            OnNetworkDespawn();
            IsSpawned = false;
            _session = null;
            NetworkId = 0;
        }

        internal void ReceiveState(ArraySegment<byte> payload)
        {
            OnNetworkState(payload);
        }

        public void SendState(NetBuffer payload, NetDelivery delivery = NetDelivery.Reliable)
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("Only the owner can send entity state.");
            }

            var envelope = new NetBuffer(payload.Length + 8);
            envelope.WriteUInt(NetworkId);
            envelope.WriteBytes(payload.AsSegment());
            _session.Bus.SendToOthers(NetMessageType.EntityState, envelope, delivery);
        }

        protected virtual void OnNetworkSpawn()
        {
        }

        protected virtual void OnNetworkDespawn()
        {
        }

        protected virtual void OnNetworkState(ArraySegment<byte> payload)
        {
        }
    }
}
