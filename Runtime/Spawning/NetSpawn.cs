using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xoderony.Networking
{
    public sealed class NetSpawn
    {
        private readonly NetSession _session;
        private readonly Dictionary<ushort, NetworkEntity> _prefabs = new Dictionary<ushort, NetworkEntity>();
        private readonly Dictionary<uint, NetworkEntity> _entities = new Dictionary<uint, NetworkEntity>();
        private uint _nextNetworkId = 1;

        internal NetSpawn(NetSession session)
        {
            _session = session;
        }

        public IReadOnlyDictionary<uint, NetworkEntity> Entities => _entities;

        public void RegisterPrefab(ushort prefabId, NetworkEntity prefab)
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
        public NetworkEntity Spawn(ushort prefabId, ulong ownerClientId, Vector3 position, Quaternion rotation, NetBuffer initialState = null)
        {
            if (!_session.IsConnected)
            {
                throw new InvalidOperationException("Session is not connected.");
            }

            if (_session.IsHost)
            {
                return SpawnAsHost(prefabId, ownerClientId, position, rotation, initialState);
            }

            var request = new NetBuffer(64);
            request.WriteUInt(0);
            request.WriteUShort(prefabId);
            request.WriteULong(ownerClientId);
            request.WriteFloat(position.x);
            request.WriteFloat(position.y);
            request.WriteFloat(position.z);
            request.WriteFloat(rotation.x);
            request.WriteFloat(rotation.y);
            request.WriteFloat(rotation.z);
            request.WriteFloat(rotation.w);
            if (initialState != null && initialState.Length > 0)
            {
                request.WriteBytes(initialState.AsSegment());
            }

            _session.Bus.SendToOthers(NetMessageType.Spawn, request, NetDelivery.Reliable);
            return null;
        }

        public void Despawn(uint networkId)
        {
            if (!_entities.TryGetValue(networkId, out var entity))
            {
                return;
            }

            if (!_session.IsHost && !entity.IsOwner)
            {
                throw new InvalidOperationException("Only host or owner can despawn.");
            }

            if (_session.IsHost)
            {
                BroadcastDespawn(networkId);
                DestroyLocal(networkId);
            }
            else
            {
                var buffer = new NetBuffer(8);
                buffer.WriteUInt(networkId);
                _session.Bus.SendToOthers(NetMessageType.Despawn, buffer, NetDelivery.Reliable);
            }
        }

        internal void ClearLocal()
        {
            var ids = new List<uint>(_entities.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                DestroyLocal(ids[i]);
            }
        }

        internal void DespawnOwnedBy(ulong clientId)
        {
            var toRemove = new List<uint>();
            foreach (var pair in _entities)
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
            foreach (var pair in _entities)
            {
                var entity = pair.Value;
                var t = entity.transform;
                var buffer = new NetBuffer(64);
                buffer.WriteUInt(entity.NetworkId);
                buffer.WriteUShort(entity.PrefabId);
                buffer.WriteULong(entity.OwnerClientId);
                buffer.WriteFloat(t.position.x);
                buffer.WriteFloat(t.position.y);
                buffer.WriteFloat(t.position.z);
                buffer.WriteFloat(t.rotation.x);
                buffer.WriteFloat(t.rotation.y);
                buffer.WriteFloat(t.rotation.z);
                buffer.WriteFloat(t.rotation.w);
                _session.Bus.SendRawToTransportPeer(
                    transportPeerId,
                    NetMessageType.Spawn,
                    NetSession.HostClientId,
                    buffer.AsSegment(),
                    NetDelivery.Reliable);
            }
        }

        internal void OnSpawnMessage(ulong senderClientId, NetBuffer reader)
        {
            var networkId = reader.ReadUInt();
            var prefabId = reader.ReadUShort();
            var ownerClientId = reader.ReadULong();
            var position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            var rotation = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            NetBuffer initial = null;
            if (reader.Position < reader.Length)
            {
                initial = new NetBuffer();
                initial.WriteBytes(reader.ReadByteSegment(reader.Length - reader.Position));
                initial.ResetRead();
            }

            if (_session.IsHost && networkId == 0)
            {
                SpawnAsHost(prefabId, ownerClientId, position, rotation, initial);
                return;
            }

            if (_entities.ContainsKey(networkId))
            {
                return;
            }

            InstantiateLocal(networkId, prefabId, ownerClientId, position, rotation, initial);
        }

        internal void OnDespawnMessage(ulong senderClientId, NetBuffer reader)
        {
            var networkId = reader.ReadUInt();
            if (_session.IsHost)
            {
                if (_entities.TryGetValue(networkId, out var entity) &&
                    entity.OwnerClientId != senderClientId &&
                    senderClientId != NetSession.HostClientId)
                {
                    return;
                }

                BroadcastDespawn(networkId);
                DestroyLocal(networkId);
                return;
            }

            DestroyLocal(networkId);
        }

        internal void OnEntityStateMessage(ulong senderClientId, NetBuffer reader)
        {
            var networkId = reader.ReadUInt();
            if (!_entities.TryGetValue(networkId, out var entity))
            {
                return;
            }

            if (entity.OwnerClientId != senderClientId)
            {
                return;
            }

            var payload = reader.ReadByteSegment(reader.Length - reader.Position);
            entity.ReceiveState(payload);
        }

        private NetworkEntity SpawnAsHost(ushort prefabId, ulong ownerClientId, Vector3 position, Quaternion rotation, NetBuffer initialState)
        {
            var networkId = _nextNetworkId++;
            var entity = InstantiateLocal(networkId, prefabId, ownerClientId, position, rotation, initialState);

            var buffer = new NetBuffer(64);
            buffer.WriteUInt(networkId);
            buffer.WriteUShort(prefabId);
            buffer.WriteULong(ownerClientId);
            buffer.WriteFloat(position.x);
            buffer.WriteFloat(position.y);
            buffer.WriteFloat(position.z);
            buffer.WriteFloat(rotation.x);
            buffer.WriteFloat(rotation.y);
            buffer.WriteFloat(rotation.z);
            buffer.WriteFloat(rotation.w);
            if (initialState != null && initialState.Length > 0)
            {
                buffer.WriteBytes(initialState.AsSegment());
            }

            _session.Bus.SendToOthers(NetMessageType.Spawn, buffer, NetDelivery.Reliable);
            return entity;
        }

        private NetworkEntity InstantiateLocal(
            uint networkId,
            ushort prefabId,
            ulong ownerClientId,
            Vector3 position,
            Quaternion rotation,
            NetBuffer initialState)
        {
            if (!_prefabs.TryGetValue(prefabId, out var prefab))
            {
                throw new InvalidOperationException($"Prefab id {prefabId} is not registered.");
            }

            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.Bind(_session, networkId, ownerClientId, prefabId);
            _entities[networkId] = instance;

            if (initialState != null && initialState.Length > 0)
            {
                instance.ReceiveState(initialState.AsSegment());
            }

            return instance;
        }

        private void BroadcastDespawn(uint networkId)
        {
            var buffer = new NetBuffer(8);
            buffer.WriteUInt(networkId);
            _session.Bus.SendToOthers(NetMessageType.Despawn, buffer, NetDelivery.Reliable);
        }

        private void DestroyLocal(uint networkId)
        {
            if (!_entities.TryGetValue(networkId, out var entity))
            {
                return;
            }

            _entities.Remove(networkId);
            entity.Unbind();
            UnityEngine.Object.Destroy(entity.gameObject);
        }
    }
}
