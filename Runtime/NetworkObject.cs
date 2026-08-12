using System;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// Network identity on a GameObject. Owner may push <see cref="SendState"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour
    {
        private NetworkManager _networkManager;
        private readonly BufferWriter _stateEnvelope = new BufferWriter(64);

        public uint NetworkObjectId { get; internal set; }
        public ulong OwnerClientId { get; internal set; }
        public bool IsSpawned { get; internal set; }
        public ushort PrefabId { get; internal set; }

        public NetworkManager NetworkManager => _networkManager;
        public bool IsOwner => IsSpawned && _networkManager != null && _networkManager.LocalClientId == OwnerClientId;

        internal void Bind(NetworkManager networkManager, uint networkObjectId, ulong ownerClientId, ushort prefabId)
        {
            _networkManager = networkManager;
            NetworkObjectId = networkObjectId;
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
            _networkManager = null;
            NetworkObjectId = 0;
        }

        internal void ReceiveState(ArraySegment<byte> payload)
        {
            OnNetworkState(payload);
        }

        public void SendState(BufferWriter payload, NetworkDelivery delivery = NetworkDelivery.Reliable)
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("Only the owner can send entity state.");
            }

            _stateEnvelope.Clear();
            _stateEnvelope.WriteUInt(NetworkObjectId);
            _stateEnvelope.WriteBytes(payload.AsSegment());
            _networkManager.CustomMessaging.SendToOthers(NetworkMessageType.EntityState, _stateEnvelope, delivery);
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
