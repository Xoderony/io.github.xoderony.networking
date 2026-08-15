using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking
{
    /// <summary>
    /// 对等会话中的网络对象生命周期：本端派生 id 并广播生成/销毁，
    /// 新对等端加入时补发本端对象（含派生对象快照），对等端离开或会话停止时清理。
    /// 构造订阅会话启停；启动时接入协议，停止时注销协议并清理本地对象。
    /// </summary>
    public sealed class NetworkObjectManager : INetworkObjectManager, INetworkObjectEvents, INetworkObjectResolver
    {
        /// <summary>Spawn 固定头：Sequence + PrefabId。</summary>
        private readonly INetworkManager _networkManager;
        private readonly INetworkObjectFactory _factory;
        private readonly Dictionary<int, NetworkObject> _prefabs = new Dictionary<int, NetworkObject>();
        private readonly Dictionary<NetworkObjectId, NetworkObject> _objects = new Dictionary<NetworkObjectId, NetworkObject>();
        private uint _nextSequence = 1;

        internal ulong LocalPeerId => _networkManager.LocalPeerId;

        public event Action<NetworkObject> Spawned;

        public event Action<NetworkObject> Despawning;

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
        /// 初始快照由对象 <c>OnSerializeSnapshot</c> 提供。
        /// </summary>
        public NetworkObject Spawn(NetworkObject instance)
        {
            Debug.Assert(!instance.IsSpawned, "Instance is already spawned.");
            Debug.Assert(instance.gameObject.scene.IsValid(), "Spawn requires a scene instance, not a prefab asset.");
            Debug.Assert(_prefabs.ContainsKey(instance.PrefabId), "Prefab is not registered.");

            var id = new NetworkObjectId(_networkManager.LocalPeerId, _nextSequence++);
            SpawnLocal(id, instance);

            var buffer = ArrayPool<byte>.Shared.Rent(NetworkMessageLimits.PayloadCapacity);
            try
            {
                _networkManager.SendToOthers(NetworkMessageType.Spawn, WriteSpawn(buffer, instance), NetworkDelivery.Reliable);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            Spawned?.Invoke(instance);
            return instance;
        }

        /// <summary>销毁本端拥有的对象并广播 Despawn。仅拥有者可调用。</summary>
        public void Despawn(NetworkObject networkObject)
        {
            Debug.Assert(networkObject.IsOwner, "Only the owner can despawn a network object.");

            var buffer = ArrayPool<byte>.Shared.Rent(NetworkMessageLimits.PayloadCapacity);
            try
            {
                var writer = new BufferWriter(buffer);
                writer.WriteUInt(networkObject.Id.Sequence);
                _networkManager.SendToOthers(NetworkMessageType.Despawn, writer.Written, NetworkDelivery.Reliable);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            DestroyLocal(networkObject.Id);
        }

        private void OnSessionStarted()
        {
            _networkManager.RegisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _networkManager.RegisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _networkManager.PeerJoined += OnPeerJoined;
            _networkManager.PeerLeft += OnPeerLeft;
        }

        private void OnSessionStopped()
        {
            _networkManager.UnregisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _networkManager.UnregisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _networkManager.PeerJoined -= OnPeerJoined;
            _networkManager.PeerLeft -= OnPeerLeft;

            foreach (var pair in _objects)
            {
                var networkObject = pair.Value;
                Despawning?.Invoke(networkObject);
                networkObject.Unbind();
                _factory.Destroy(networkObject);
            }

            _objects.Clear();
            _nextSequence = 1;
        }

        private void OnPeerJoined(ulong peerId)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(NetworkMessageLimits.PayloadCapacity);
            try
            {
                foreach (var pair in _objects)
                {
                    var networkObject = pair.Value;
                    if (networkObject.Id.PeerId != _networkManager.LocalPeerId)
                    {
                        continue;
                    }

                    _networkManager.SendToPeer(peerId, NetworkMessageType.Spawn, WriteSpawn(buffer, networkObject), NetworkDelivery.Reliable);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
            instance.DeserializeSnapshot(ref reader);
            Spawned?.Invoke(instance);
        }

        private void OnDespawnMessage(ulong senderPeerId, BufferReader reader)
        {
            DestroyLocal(new NetworkObjectId(senderPeerId, reader.ReadUInt()));
        }

        private static ReadOnlySpan<byte> WriteSpawn(byte[] buffer, NetworkObject networkObject)
        {
            var writer = new BufferWriter(buffer);
            writer.WriteUInt(networkObject.Id.Sequence);
            writer.WriteInt(networkObject.PrefabId);
            networkObject.SerializeSnapshot(ref writer);
            return writer.Written;
        }

        private void SpawnLocal(in NetworkObjectId id, NetworkObject instance)
        {
            instance.Bind(this, id);
            _objects[id] = instance;
        }

        private void DestroyLocal(in NetworkObjectId id)
        {
            if (!_objects.TryGetValue(id, out var networkObject))
            {
                return;
            }

            Despawning?.Invoke(networkObject);
            _objects.Remove(id);
            networkObject.Unbind();
            _factory.Destroy(networkObject);
        }
    }
}
