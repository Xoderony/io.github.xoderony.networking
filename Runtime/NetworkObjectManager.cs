using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// 对等会话中的网络对象复制：本端派生 id 并广播生成/销毁，投递对象通道，
    /// 新对等端加入时补发本端对象（含当前状态），对等端离开或会话停止时清理。
    /// 构造订阅会话启停；启动时接入协议，停止时注销协议并清理本地对象。
    /// </summary>
    public sealed class NetworkObjectManager : INetworkObjectManager
    {
        /// <summary>Spawn 固定头：Sequence + PrefabId。</summary>
        private const int SpawnHeaderSize = sizeof(uint) + sizeof(int);

        /// <summary>Spawn 写入容量：固定头 + 对象当前状态。</summary>
        private const int SpawnBufferCapacity = SpawnHeaderSize + NetworkMessageLimits.StateDataCapacity;

        private readonly INetworkManager _networkManager;
        private readonly INetworkObjectFactory _factory;
        private readonly Dictionary<int, NetworkObject> _prefabs = new Dictionary<int, NetworkObject>();
        private readonly Dictionary<NetworkObjectId, NetworkObject> _objects = new Dictionary<NetworkObjectId, NetworkObject>();
        private readonly byte[] _spawnBuffer = new byte[SpawnBufferCapacity];
        private uint _nextSequence = 1;

        public NetworkObjectManager(INetworkManager networkManager, INetworkObjectFactory factory)
        {
            _networkManager = networkManager;
            _factory = factory;
            networkManager.Started += OnSessionStarted;
            networkManager.Stopped += OnSessionStopped;
        }

        public void RegisterPrefab(NetworkObject prefab)
        {
            var prefabId = Animator.StringToHash(prefab.gameObject.name);
            Debug.Assert(prefabId != 0, "Prefab name hashed to reserved id 0.");
            if (_prefabs.TryGetValue(prefabId, out var existing))
            {
                Debug.Assert(existing == prefab, "Prefab id collision.");
            }

            prefab.PrefabId = prefabId;
            _prefabs[prefabId] = prefab;
        }

        public void UnregisterPrefab(NetworkObject prefab)
        {
            _prefabs.Remove(prefab.PrefabId);
        }

        public bool TryGetPrefab(int prefabId, out NetworkObject prefab)
        {
            return _prefabs.TryGetValue(prefabId, out prefab);
        }

        public bool TryGetSpawned(in NetworkObjectId id, out NetworkObject spawned)
        {
            return _objects.TryGetValue(id, out spawned);
        }

        /// <summary>
        /// 将调用方已创建的实例以本机为拥有者入网并广播。创建与初始字段由外部完成。
        /// 快照由对象上的状态变量列表写入。
        /// </summary>
        public NetworkObject Spawn(NetworkObject instance)
        {
            Debug.Assert(!instance.IsSpawned, "Instance is already spawned.");
            Debug.Assert(instance.gameObject.scene.IsValid(), "Spawn requires a scene instance, not a prefab asset.");
            Debug.Assert(_prefabs.ContainsKey(instance.PrefabId), "Prefab is not registered.");

            var id = new NetworkObjectId(_networkManager.LocalPeerId, _nextSequence++);
            SpawnLocal(id, instance);

            var writer = new BufferWriter(_spawnBuffer);
            writer.WriteUInt(id.Sequence);
            writer.WriteInt(instance.PrefabId);
            instance.WriteSnapshot(ref writer);
            _networkManager.SendToOthers(NetworkMessageType.Spawn, writer, NetworkDelivery.Reliable);
            return instance;
        }

        /// <summary>销毁本端拥有的对象并广播 Despawn。仅拥有者可调用。</summary>
        public void Despawn(NetworkObject networkObject)
        {
            Debug.Assert(networkObject.IsOwner, "Only the owner can despawn a network object.");

            var writer = new BufferWriter(_spawnBuffer);
            writer.WriteUInt(networkObject.Id.Sequence);
            _networkManager.SendToOthers(NetworkMessageType.Despawn, writer, NetworkDelivery.Reliable);
            DestroyLocal(networkObject.Id);
        }

        public void Flush()
        {
            foreach (var pair in _objects)
            {
                var networkObject = pair.Value;
                if (networkObject.Id.PeerId != _networkManager.LocalPeerId)
                {
                    continue;
                }

                networkObject.FlushDirty();
            }
        }

        private void OnSessionStarted()
        {
            _networkManager.RegisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _networkManager.RegisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _networkManager.RegisterMessage(NetworkMessageType.State, OnStateMessage);
            _networkManager.RegisterMessage(NetworkMessageType.Rpc, OnRpcMessage);
            _networkManager.PeerJoined += OnPeerJoined;
            _networkManager.PeerLeft += OnPeerLeft;
        }

        private void OnSessionStopped()
        {
            _networkManager.UnregisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.State, OnStateMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.Rpc, OnRpcMessage);
            _networkManager.PeerJoined -= OnPeerJoined;
            _networkManager.PeerLeft -= OnPeerLeft;

            foreach (var pair in _objects)
            {
                pair.Value.Unbind();
                _factory.Destroy(pair.Value);
            }

            _objects.Clear();
            _nextSequence = 1;
        }

        private void OnPeerJoined(ulong peerId)
        {
            foreach (var pair in _objects)
            {
                var networkObject = pair.Value;
                if (networkObject.Id.PeerId != _networkManager.LocalPeerId)
                {
                    continue;
                }

                var writer = new BufferWriter(_spawnBuffer);
                writer.WriteUInt(networkObject.Id.Sequence);
                writer.WriteInt(networkObject.PrefabId);
                networkObject.WriteSnapshot(ref writer);
                _networkManager.SendToPeer(peerId, NetworkMessageType.Spawn, writer, NetworkDelivery.Reliable);
            }
        }

        private void OnPeerLeft(ulong peerId)
        {
            var ids = new List<NetworkObjectId>();
            foreach (var pair in _objects)
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

        private void OnSpawnMessage(ulong senderPeerId, BufferReader reader)
        {
            var id = new NetworkObjectId(senderPeerId, reader.ReadUInt());
            var prefabId = reader.ReadInt();
            Debug.Assert(!_objects.ContainsKey(id), "Duplicate spawn.");

            _prefabs.TryGetValue(prefabId, out var prefab);
            Debug.Assert(prefab != null, $"Prefab id {prefabId} is not registered.");

            var instance = _factory.Create(prefab);
            SpawnLocal(id, instance);
            instance.ApplySnapshot(reader);
        }

        private void OnDespawnMessage(ulong senderPeerId, BufferReader reader)
        {
            DestroyLocal(new NetworkObjectId(senderPeerId, reader.ReadUInt()));
        }

        private void OnStateMessage(ulong senderPeerId, BufferReader reader)
        {
            var id = new NetworkObjectId(senderPeerId, reader.ReadUInt());
            if (!_objects.TryGetValue(id, out var networkObject))
            {
                return;
            }

            networkObject.ReceiveState(reader);
        }

        private void OnRpcMessage(ulong senderPeerId, BufferReader reader)
        {
            var id = new NetworkObjectId(senderPeerId, reader.ReadUInt());
            if (!_objects.TryGetValue(id, out var networkObject))
            {
                return;
            }

            networkObject.ReceiveRpc(senderPeerId, reader);
        }

        private void SpawnLocal(in NetworkObjectId id, NetworkObject instance)
        {
            instance.Bind(_networkManager, id);
            _objects[id] = instance;
        }

        private void DestroyLocal(in NetworkObjectId id)
        {
            if (!_objects.Remove(id, out var networkObject))
            {
                return;
            }

            networkObject.Unbind();
            _factory.Destroy(networkObject);
        }
    }
}
