using System;
using System.Collections.Generic;
using UnityEngine;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>
    /// 对等会话中的网络对象生命周期：本端派生 id 并广播生成/销毁，
    /// 成员经 Session 承认后补发本端对象快照，成员离开或会话停止时清理。
    /// </summary>
    public sealed class NetworkObjectManager : INetworkObjectManager, IDisposable {
        /// <summary>Spawn 固定头：Id + PrefabId。</summary>
        private readonly INetworkTransport _transport;
        private readonly INetworkSession _session;
        private readonly INetworkMessageManager _messageManager;
        private readonly INetworkObjectIdAllocator _idAllocator;
        private readonly INetworkObjectFactory _factory;
        private readonly Dictionary<int, NetworkObject> _prefabs = new Dictionary<int, NetworkObject>();
        private readonly Dictionary<uint, NetworkObject> _objects = new Dictionary<uint, NetworkObject>();

        internal ulong LocalPeerId => _transport.LocalPeerId;

        public event Action<NetworkObject, uint> Spawned;

        public event Action<NetworkObject, uint> Despawned;

        public NetworkObjectManager(INetworkTransport transport, INetworkSession session, INetworkMessageManager messageManager, INetworkObjectIdAllocator idAllocator, INetworkObjectFactory factory) {
            _transport = transport;
            _session = session;
            _messageManager = messageManager;
            _idAllocator = idAllocator;
            _factory = factory;
            messageManager.RegisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            messageManager.RegisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            session.MemberJoined += OnMemberJoined;
            session.MemberLeft += OnMemberLeft;
            session.Stopped += OnSessionStopped;
        }

        public void RegisterPrefab(NetworkObject prefab) {
            var prefabId = Animator.StringToHash(prefab.gameObject.name);
            Debug.Assert(prefabId != 0, "Prefab name hashed to reserved id 0.");
            if (_prefabs.TryGetValue(prefabId, out var existing)) {
                Debug.Assert(existing == prefab, "Prefab id collision.");
            }

            prefab.PrefabId = prefabId;
            _prefabs[prefabId] = prefab;
        }

        public void UnregisterPrefab(NetworkObject prefab) {
            _prefabs.Remove(prefab.PrefabId);
        }

        public bool TryGetPrefab(int prefabId, out NetworkObject prefab) {
            return _prefabs.TryGetValue(prefabId, out prefab);
        }

        public bool TryGetSpawned(uint id, out NetworkObject spawned) {
            return _objects.TryGetValue(id, out spawned);
        }

        /// <summary>
        /// 由工厂创建 Prefab 实例，调用初始化委托后以本机为拥有者入网并广播。
        /// 初始快照在绑定网络身份后序列化，并在发布 <see cref="Spawned"/> 前发送。
        /// </summary>
        public NetworkObject Spawn(NetworkObject prefab, Action<NetworkObject> initialize = null) {
            Debug.Assert(!prefab.gameObject.scene.IsValid(), "Spawn requires a prefab asset, not a scene instance.");
            Debug.Assert(_prefabs.TryGetValue(prefab.PrefabId, out var registeredPrefab) && registeredPrefab == prefab, "Prefab is not registered.");

            var instance = _factory.Create(prefab);
            initialize?.Invoke(instance);
            var id = _idAllocator.Allocate();
            SpawnLocal(id, _transport.LocalPeerId, instance);

            Span<byte> buffer = stackalloc byte[NetworkMessageLimits.MessageCapacity];
            var writer = new BufferWriter(buffer);
            writer.WriteByte(NetworkMessageType.Spawn);
            writer.WriteUInt(instance.Id);
            writer.WriteInt(instance.PrefabId);
            instance.SerializeSnapshot(ref writer);
            _messageManager.SendToOthers(writer.Written, NetworkDelivery.Reliable);

            Spawned?.Invoke(instance, id);
            return instance;
        }

        /// <summary>销毁本端拥有的对象并广播 Despawn。仅拥有者可调用。</summary>
        public void Despawn(NetworkObject networkObject) {
            Debug.Assert(networkObject.IsOwner, "Only the owner can despawn a network object.");

            Span<byte> buffer = stackalloc byte[NetworkMessageLimits.MessageCapacity];
            var writer = new BufferWriter(buffer);
            writer.WriteByte(NetworkMessageType.Despawn);
            writer.WriteUInt(networkObject.Id);
            _messageManager.SendToOthers(writer.Written, NetworkDelivery.Reliable);

            DestroyLocal(networkObject.Id);
        }

        public void Dispose() {
            _messageManager.UnregisterMessage(NetworkMessageType.Spawn, OnSpawnMessage);
            _messageManager.UnregisterMessage(NetworkMessageType.Despawn, OnDespawnMessage);
            _session.MemberJoined -= OnMemberJoined;
            _session.MemberLeft -= OnMemberLeft;
            _session.Stopped -= OnSessionStopped;
        }

        private void OnSessionStopped() {
            var ids = new List<uint>(_objects.Count);
            foreach (var pair in _objects) {
                ids.Add(pair.Key);
            }

            foreach (var id in ids) {
                DestroyLocal(id);
            }
        }

        private void OnMemberJoined(ulong peerId) {
            Span<byte> buffer = stackalloc byte[NetworkMessageLimits.MessageCapacity];
            foreach (var pair in _objects) {
                var networkObject = pair.Value;
                if (networkObject.OwnerPeerId != _transport.LocalPeerId) {
                    continue;
                }

                var writer = new BufferWriter(buffer);
                writer.WriteByte(NetworkMessageType.Spawn);
                writer.WriteUInt(networkObject.Id);
                writer.WriteInt(networkObject.PrefabId);
                networkObject.SerializeSnapshot(ref writer);
                _messageManager.SendToPeer(peerId, writer.Written, NetworkDelivery.Reliable);
            }
        }

        private void OnMemberLeft(ulong peerId) {
            var ids = new List<uint>();
            foreach (var pair in _objects) {
                if (pair.Value.OwnerPeerId == peerId) {
                    ids.Add(pair.Key);
                }
            }

            foreach (var id in ids) {
                DestroyLocal(id);
            }
        }

        private void OnSpawnMessage(ulong senderPeerId, BufferReader reader) {
            var id = reader.ReadUInt();
            var prefabId = reader.ReadInt();

            if (_objects.TryGetValue(id, out var existing)) {
                if (existing.OwnerPeerId != senderPeerId) {
                    Debug.Assert(false, "Network object id collision between different owners.");
                    return;
                }

                if (existing.PrefabId != prefabId) {
                    Debug.Assert(false, "Spawn snapshot prefab does not match the existing object.");
                    return;
                }

                existing.DeserializeSnapshot(ref reader);
                return;
            }

            _prefabs.TryGetValue(prefabId, out var prefab);
            Debug.Assert(prefab != null, $"Prefab id {prefabId} is not registered.");

            var instance = _factory.Create(prefab);
            SpawnLocal(id, senderPeerId, instance);
            instance.DeserializeSnapshot(ref reader);
            Spawned?.Invoke(instance, id);
        }

        private void OnDespawnMessage(ulong senderPeerId, BufferReader reader) {
            var id = reader.ReadUInt();
            if (!_objects.TryGetValue(id, out var networkObject)) {
                return;
            }

            if (networkObject.OwnerPeerId != senderPeerId) {
                Debug.Assert(false, "Only the current owner can despawn a network object.");
                return;
            }

            DestroyLocal(id);
        }

        private void SpawnLocal(uint id, ulong ownerPeerId, NetworkObject instance) {
            Debug.Assert(id != 0, "Network object id 0 is reserved.");
            Debug.Assert(ownerPeerId != 0, "Network object owner PeerId 0 is invalid.");
            Debug.Assert(!_objects.ContainsKey(id), "Network object id is already in use.");
            instance.Bind(this, id, ownerPeerId);
            _objects[id] = instance;
        }

        private void DestroyLocal(uint id) {
            if (!_objects.TryGetValue(id, out var networkObject)) {
                return;
            }

            _objects.Remove(id);
            networkObject.Unbind();
            Despawned?.Invoke(networkObject, id);
            _factory.Destroy(networkObject);
        }
    }
}
