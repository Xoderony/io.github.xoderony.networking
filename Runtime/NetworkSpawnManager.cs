using System;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// 对等网格生成管理：本端对象由本端派生 id 并广播 Spawn，无主机、无中继。
    /// 新对等端加入时向其补发本端拥有的对象；对等端离开或会话停止时清理本地对象。
    /// 构造即接入会话（注册 Spawn/Despawn/EntityState 与 Peer*/Stopped），会话停止时自动注销。
    /// </summary>
    public sealed class NetworkSpawnManager
    {
        /// <summary>Spawn 固定头：LocalId + PrefabId + 位置/旋转。</summary>
        private const int SpawnHeaderSize = sizeof(uint) + sizeof(ushort) + sizeof(float) * 7;

        /// <summary>Spawn 信封容量：固定头 + 初始状态（上限见 <see cref="NetworkMessageLimits.StateDataCapacity"/>）。</summary>
        private const int SpawnEnvelopeCapacity = SpawnHeaderSize + NetworkMessageLimits.StateDataCapacity;

        private readonly NetworkManager _networkManager;
        private readonly Dictionary<ushort, NetworkObject> _prefabs = new Dictionary<ushort, NetworkObject>();
        private readonly Dictionary<NetworkObjectId, NetworkObject> _networkObjects = new Dictionary<NetworkObjectId, NetworkObject>();
        private readonly byte[] _spawnBuffer = new byte[SpawnEnvelopeCapacity];
        private uint _nextLocalId = 1;

        public NetworkSpawnManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            networkManager.RegisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            networkManager.RegisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            networkManager.RegisterMessage(NetworkMessageType.EntityState, OnEntityStateMessage);
            networkManager.PeerJoined += OnPeerJoined;
            networkManager.PeerLeft += OnPeerLeft;
            networkManager.Stopped += OnSessionStopped;
        }

        public IReadOnlyDictionary<NetworkObjectId, NetworkObject> SpawnedObjects => _networkObjects;

        public void RegisterPrefab(ushort prefabId, NetworkObject prefab)
        {
            _prefabs[prefabId] = prefab;
        }

        /// <summary>
        /// 以本机为拥有者生成对象并广播给所有对端。id 由本端派生（见 <see cref="NetworkObjectId"/>），
        /// 初始状态字节数受基础协议数据上限约束（见 <see cref="NetworkMessageLimits.StateDataCapacity"/>）。
        /// </summary>
        public NetworkObject Spawn(
            ushort prefabId,
            Vector3 position,
            Quaternion rotation,
            BufferWriter initialState = default)
        {
            var id = new NetworkObjectId(_networkManager.LocalPeerId, _nextLocalId++);
            var instance = InstantiateLocal(id, prefabId, position, rotation, initialState.Written);

            var writer = new BufferWriter(_spawnBuffer);
            writer.WriteUInt(id.LocalId);
            writer.WriteUShort(prefabId);
            writer.WriteFloat(position.x);
            writer.WriteFloat(position.y);
            writer.WriteFloat(position.z);
            writer.WriteFloat(rotation.x);
            writer.WriteFloat(rotation.y);
            writer.WriteFloat(rotation.z);
            writer.WriteFloat(rotation.w);
            if (initialState.DataLength > 0)
            {
                writer.WriteBytes(initialState.Written);
            }

            _networkManager.SendToOthers(NetworkMessageType.Spawn, writer, NetworkDelivery.Reliable);
            return instance;
        }

        /// <summary>销毁本端拥有的对象并广播 Despawn。仅拥有者可调用。</summary>
        public void Despawn(NetworkObject networkObject)
        {
            Debug.Assert(networkObject.IsOwner, "Only the owner can despawn a network object.");

            var writer = new BufferWriter(_spawnBuffer);
            writer.WriteUInt(networkObject.Id.LocalId);
            _networkManager.SendToOthers(NetworkMessageType.Despawn, writer, NetworkDelivery.Reliable);
            DestroyLocal(networkObject.Id);
        }

        private void OnPeerJoined(ulong peerId)
        {
            var owned = new List<NetworkObject>(_networkObjects.Count);
            foreach (var pair in _networkObjects)
            {
                if (pair.Value.Id.PeerId == _networkManager.LocalPeerId)
                {
                    owned.Add(pair.Value);
                }
            }

            foreach (var networkObject in owned)
            {
                SendSpawnTo(peerId, networkObject);
            }
        }

        private void OnPeerLeft(ulong peerId)
        {
            var ids = new List<NetworkObjectId>(_networkObjects.Count);
            foreach (var pair in _networkObjects)
            {
                if (pair.Value.Id.PeerId == peerId)
                {
                    ids.Add(pair.Key);
                }
            }

            foreach (var id in ids)
            {
                DestroyLocal(id);
            }
        }

        private void OnSessionStopped()
        {
            _networkManager.UnregisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.EntityState, OnEntityStateMessage);
            _networkManager.PeerJoined -= OnPeerJoined;
            _networkManager.PeerLeft -= OnPeerLeft;
            _networkManager.Stopped -= OnSessionStopped;
            ClearLocal();
        }

        private void OnSpawnMessage(ulong senderPeerId, BufferReader reader)
        {
            var id = new NetworkObjectId(senderPeerId, reader.ReadUInt());
            var prefabId = reader.ReadUShort();
            var position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            var rotation = new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
            if (_networkObjects.ContainsKey(id))
            {
                return;
            }

            InstantiateLocal(id, prefabId, position, rotation, reader.Buffer[reader.Position..]);
        }

        private void OnDespawnMessage(ulong senderPeerId, BufferReader reader)
        {
            DestroyLocal(new NetworkObjectId(senderPeerId, reader.ReadUInt()));
        }

        private void OnEntityStateMessage(ulong senderPeerId, BufferReader reader)
        {
            var id = new NetworkObjectId(senderPeerId, reader.ReadUInt());
            if (!_networkObjects.TryGetValue(id, out var networkObject))
            {
                return;
            }

            networkObject.ReceiveState(reader.Buffer[reader.Position..]);
        }

        private void SendSpawnTo(ulong peerId, NetworkObject networkObject)
        {
            var t = networkObject.transform;
            var writer = new BufferWriter(_spawnBuffer);
            writer.WriteUInt(networkObject.Id.LocalId);
            writer.WriteUShort(networkObject.PrefabId);
            writer.WriteFloat(t.position.x);
            writer.WriteFloat(t.position.y);
            writer.WriteFloat(t.position.z);
            writer.WriteFloat(t.rotation.x);
            writer.WriteFloat(t.rotation.y);
            writer.WriteFloat(t.rotation.z);
            writer.WriteFloat(t.rotation.w);
            _networkManager.SendToPeer(peerId, NetworkMessageType.Spawn, writer, NetworkDelivery.Reliable);
        }

        private NetworkObject InstantiateLocal(
            NetworkObjectId id,
            ushort prefabId,
            Vector3 position,
            Quaternion rotation,
            ReadOnlySpan<byte> initialState)
        {
            _prefabs.TryGetValue(prefabId, out var prefab);
            Debug.Assert(prefab != null, $"Prefab id {prefabId} is not registered.");

            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.Bind(_networkManager, id, prefabId);
            _networkObjects[id] = instance;
            if (!initialState.IsEmpty)
            {
                instance.ReceiveState(initialState);
            }

            return instance;
        }

        private void DestroyLocal(NetworkObjectId id)
        {
            if (!_networkObjects.TryGetValue(id, out var networkObject))
            {
                return;
            }

            _networkObjects.Remove(id);
            networkObject.Unbind();
            UnityEngine.Object.Destroy(networkObject.gameObject);
        }

        private void ClearLocal()
        {
            var ids = new List<NetworkObjectId>(_networkObjects.Count);
            foreach (var id in _networkObjects.Keys)
            {
                ids.Add(id);
            }

            foreach (var id in ids)
            {
                DestroyLocal(id);
            }
        }
    }
}
