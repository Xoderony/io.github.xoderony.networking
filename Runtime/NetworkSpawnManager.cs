using System;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    public sealed class NetworkSpawnManager
    {
        private readonly NetworkManager _networkManager;
        private readonly Dictionary<ushort, NetworkObject> _prefabs = new Dictionary<ushort, NetworkObject>();
        private readonly Dictionary<uint, NetworkObject> _networkObjects = new Dictionary<uint, NetworkObject>();
        private readonly BufferWriter _scratch = new BufferWriter(64);
        private readonly BufferWriter _initialScratch = new BufferWriter(64);
        private uint _nextNetworkObjectId = 1;

        internal NetworkSpawnManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
        }

        public IReadOnlyDictionary<uint, NetworkObject> SpawnedObjects => _networkObjects;

        public void RegisterPrefab(ushort prefabId, NetworkObject prefab)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            _prefabs[prefabId] = prefab;
        }

        /// <summary>
        /// Spawn a prefab owned by <paramref name="ownerClientId"/>.
        /// Host allocates ids; clients request via Host relay of Spawn message with id 0.
        /// </summary>
        public NetworkObject Spawn(
            ushort prefabId,
            ulong ownerClientId,
            Vector3 position,
            Quaternion rotation,
            BufferWriter initialState = null)
        {
            if (!_networkManager.IsConnected)
            {
                throw new InvalidOperationException("Session is not connected.");
            }

            if (_networkManager.IsHost)
            {
                return SpawnAsHost(prefabId, ownerClientId, position, rotation, initialState);
            }

            _scratch.Clear();
            _scratch.WriteUInt(0);
            _scratch.WriteUShort(prefabId);
            _scratch.WriteULong(ownerClientId);
            _scratch.WriteFloat(position.x);
            _scratch.WriteFloat(position.y);
            _scratch.WriteFloat(position.z);
            _scratch.WriteFloat(rotation.x);
            _scratch.WriteFloat(rotation.y);
            _scratch.WriteFloat(rotation.z);
            _scratch.WriteFloat(rotation.w);
            if (initialState != null && initialState.Length > 0)
            {
                _scratch.WriteBytes(initialState.AsSegment());
            }

            _networkManager.CustomMessaging.SendToOthers(NetworkMessageType.Spawn, _scratch, NetworkDelivery.Reliable);
            return null;
        }

        public void Despawn(uint networkObjectId)
        {
            if (!_networkObjects.TryGetValue(networkObjectId, out var networkObject))
            {
                return;
            }

            if (!_networkManager.IsHost && !networkObject.IsOwner)
            {
                throw new InvalidOperationException("Only host or owner can despawn.");
            }

            if (_networkManager.IsHost)
            {
                BroadcastDespawn(networkObjectId);
                DestroyLocal(networkObjectId);
            }
            else
            {
                _scratch.Clear();
                _scratch.WriteUInt(networkObjectId);
                _networkManager.CustomMessaging.SendToOthers(NetworkMessageType.Despawn, _scratch, NetworkDelivery.Reliable);
            }
        }

        internal void ClearLocal()
        {
            var ids = new List<uint>(_networkObjects.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                DestroyLocal(ids[i]);
            }
        }

        internal void DespawnOwnedBy(ulong clientId)
        {
            var toRemove = new List<uint>();
            foreach (var pair in _networkObjects)
            {
                if (pair.Value.OwnerClientId == clientId)
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRemove.Count; i++)
            {
                BroadcastDespawn(toRemove[i]);
                DestroyLocal(toRemove[i]);
            }
        }

        internal void SendSnapshotTo(ulong transportPeerId)
        {
            foreach (var pair in _networkObjects)
            {
                var networkObject = pair.Value;
                var t = networkObject.transform;
                _scratch.Clear();
                _scratch.WriteUInt(networkObject.NetworkObjectId);
                _scratch.WriteUShort(networkObject.PrefabId);
                _scratch.WriteULong(networkObject.OwnerClientId);
                _scratch.WriteFloat(t.position.x);
                _scratch.WriteFloat(t.position.y);
                _scratch.WriteFloat(t.position.z);
                _scratch.WriteFloat(t.rotation.x);
                _scratch.WriteFloat(t.rotation.y);
                _scratch.WriteFloat(t.rotation.z);
                _scratch.WriteFloat(t.rotation.w);
                _networkManager.CustomMessaging.SendRawToTransportPeer(
                    transportPeerId,
                    NetworkMessageType.Spawn,
                    NetworkManager.ServerClientId,
                    _scratch.AsSegment(),
                    NetworkDelivery.Reliable);
            }
        }

        internal void OnSpawnMessage(ulong senderClientId, BufferReader reader)
        {
            var networkObjectId = reader.ReadUInt();
            var prefabId = reader.ReadUShort();
            var ownerClientId = reader.ReadULong();
            var position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            var rotation = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            BufferWriter initial = null;
            if (reader.Position < reader.Length)
            {
                var remaining = reader.ReadByteSegment(reader.Length - reader.Position);
                _initialScratch.Clear();
                _initialScratch.WriteBytes(remaining);
                initial = _initialScratch;
            }

            if (_networkManager.IsHost && networkObjectId == 0)
            {
                SpawnAsHost(prefabId, ownerClientId, position, rotation, initial);
                return;
            }

            if (_networkObjects.ContainsKey(networkObjectId))
            {
                return;
            }

            InstantiateLocal(networkObjectId, prefabId, ownerClientId, position, rotation, initial);
        }

        internal void OnDespawnMessage(ulong senderClientId, BufferReader reader)
        {
            var networkObjectId = reader.ReadUInt();
            if (_networkManager.IsHost)
            {
                if (_networkObjects.TryGetValue(networkObjectId, out var networkObject) &&
                    networkObject.OwnerClientId != senderClientId &&
                    senderClientId != NetworkManager.ServerClientId)
                {
                    return;
                }

                BroadcastDespawn(networkObjectId);
                DestroyLocal(networkObjectId);
                return;
            }

            DestroyLocal(networkObjectId);
        }

        internal void OnEntityStateMessage(ulong senderClientId, BufferReader reader)
        {
            var networkObjectId = reader.ReadUInt();
            if (!_networkObjects.TryGetValue(networkObjectId, out var networkObject))
            {
                return;
            }

            if (networkObject.OwnerClientId != senderClientId)
            {
                return;
            }

            var payload = reader.ReadByteSegment(reader.Length - reader.Position);
            networkObject.ReceiveState(payload);
        }

        private NetworkObject SpawnAsHost(
            ushort prefabId,
            ulong ownerClientId,
            Vector3 position,
            Quaternion rotation,
            BufferWriter initialState)
        {
            var networkObjectId = _nextNetworkObjectId++;
            var networkObject = InstantiateLocal(networkObjectId, prefabId, ownerClientId, position, rotation, initialState);

            _scratch.Clear();
            _scratch.WriteUInt(networkObjectId);
            _scratch.WriteUShort(prefabId);
            _scratch.WriteULong(ownerClientId);
            _scratch.WriteFloat(position.x);
            _scratch.WriteFloat(position.y);
            _scratch.WriteFloat(position.z);
            _scratch.WriteFloat(rotation.x);
            _scratch.WriteFloat(rotation.y);
            _scratch.WriteFloat(rotation.z);
            _scratch.WriteFloat(rotation.w);
            if (initialState != null && initialState.Length > 0)
            {
                _scratch.WriteBytes(initialState.AsSegment());
            }

            _networkManager.CustomMessaging.SendToOthers(NetworkMessageType.Spawn, _scratch, NetworkDelivery.Reliable);
            return networkObject;
        }

        private NetworkObject InstantiateLocal(
            uint networkObjectId,
            ushort prefabId,
            ulong ownerClientId,
            Vector3 position,
            Quaternion rotation,
            BufferWriter initialState)
        {
            if (!_prefabs.TryGetValue(prefabId, out var prefab))
            {
                throw new InvalidOperationException($"Prefab id {prefabId} is not registered.");
            }

            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.Bind(_networkManager, networkObjectId, ownerClientId, prefabId);
            _networkObjects[networkObjectId] = instance;

            if (initialState != null && initialState.Length > 0)
            {
                instance.ReceiveState(initialState.AsSegment());
            }

            return instance;
        }

        private void BroadcastDespawn(uint networkObjectId)
        {
            _scratch.Clear();
            _scratch.WriteUInt(networkObjectId);
            _networkManager.CustomMessaging.SendToOthers(NetworkMessageType.Despawn, _scratch, NetworkDelivery.Reliable);
        }

        private void DestroyLocal(uint networkObjectId)
        {
            if (!_networkObjects.TryGetValue(networkObjectId, out var networkObject))
            {
                return;
            }

            _networkObjects.Remove(networkObjectId);
            networkObject.Unbind();
            UnityEngine.Object.Destroy(networkObject.gameObject);
        }
    }
}
