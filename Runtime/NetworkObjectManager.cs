using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Xoderony.Networking.Messaging;
using Xoderony.Networking.Serialization;
using Xoderony.Networking.Transport;

namespace Xoderony.Networking {
    /// <summary>
    /// 对等会话中的网络对象生命周期：本端派生 id 并广播生成/销毁，
    /// 成员经 Session 承认后补发本端对象快照，成员离开或会话停止时清理。
    /// 会话房主切换时，将原房主的持久对象权威迁到新房主，不改 Id。
    /// </summary>
    public sealed class NetworkObjectManager : INetworkObjectManager, IDisposable {
        /// <summary>Spawn 固定头：Id + PrefabId + PersistOnOwnerLeave。</summary>
        private readonly INetworkTransport _transport;
        private readonly INetworkSession _session;
        private readonly INetworkMessageManager _messageManager;
        private readonly INetworkObjectIdAllocator _idAllocator;
        private readonly INetworkObjectFactory _factory;
        private readonly Dictionary<int, NetworkObject> _prefabs = new Dictionary<int, NetworkObject>();
        private readonly Dictionary<uint, NetworkObject> _objects = new Dictionary<uint, NetworkObject>();

        internal ulong LocalPeerId => _transport.LocalPeerId;

        public event Action<NetworkObject> Spawned;

        public event Action<NetworkObject> Despawned;

        public event Action<NetworkObject> OwnerChanged;

        public NetworkObjectManager(INetworkTransport transport, INetworkSession session, INetworkMessageManager messageManager, INetworkObjectIdAllocator idAllocator, INetworkObjectFactory factory) {
            _transport = transport;
            _session = session;
            _messageManager = messageManager;
            _idAllocator = idAllocator;
            _factory = factory;
            messageManager.RegisterHandler(NetworkMessageType.Spawn, OnSpawnMessage);
            messageManager.RegisterHandler(NetworkMessageType.Despawn, OnDespawnMessage);
            session.MemberJoined += OnMemberJoined;
            session.MemberLeft += OnMemberLeft;
            session.OwnerChanged += OnSessionOwnerChanged;
            session.Stopped += OnSessionStopped;
        }

        public void RegisterPrefab(NetworkObject prefab) {
            var prefabId = Animator.StringToHash(prefab.gameObject.name);
            Assert.AreNotEqual(0, prefabId, "Prefab name hashed to reserved id 0.");
            Assert.IsTrue(!_prefabs.TryGetValue(prefabId, out var existing) || existing == prefab, "Prefab id collision.");

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
        /// 先发送初始快照，再绑定网络身份并发布 <see cref="Spawned"/>。
        /// </summary>
        public NetworkObject Spawn(NetworkObject prefab, Action<NetworkObject> initialize = null) {
            Assert.IsFalse(prefab.gameObject.scene.IsValid(), "Spawn requires a prefab asset, not a scene instance.");
            _prefabs.TryGetValue(prefab.PrefabId, out var registeredPrefab);
            Assert.AreEqual(prefab, registeredPrefab, "Prefab is not registered.");

            var instance = _factory.Instantiate(prefab);
            initialize?.Invoke(instance);
            var id = _idAllocator.Allocate();

            Span<byte> buffer = stackalloc byte[NetworkMessageLimits.MessageCapacity];
            var writer = new BufferWriter(buffer);
            WriteSpawn(ref writer, id, instance);
            _messageManager.SendToOthers(writer.Written, NetworkDelivery.Reliable);

            SpawnLocal(id, _transport.LocalPeerId, instance);
            return instance;
        }

        /// <summary>销毁本端拥有的对象并广播 Despawn。仅拥有者可调用。</summary>
        public void Despawn(NetworkObject networkObject) {
            Assert.IsTrue(networkObject.IsOwner, "Only the owner can despawn a network object.");

            Span<byte> buffer = stackalloc byte[NetworkMessageLimits.MessageCapacity];
            var writer = new BufferWriter(buffer);
            writer.WriteByte(NetworkMessageType.Despawn);
            writer.WriteUInt(networkObject.Id);
            _messageManager.SendToOthers(writer.Written, NetworkDelivery.Reliable);

            DestroyLocal(networkObject.Id);
        }

        public void Dispose() {
            _messageManager.UnregisterHandler(NetworkMessageType.Spawn, OnSpawnMessage);
            _messageManager.UnregisterHandler(NetworkMessageType.Despawn, OnDespawnMessage);
            _session.MemberJoined -= OnMemberJoined;
            _session.MemberLeft -= OnMemberLeft;
            _session.OwnerChanged -= OnSessionOwnerChanged;
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

        private void OnSessionOwnerChanged(ulong previousOwnerPeerId, ulong ownerPeerId) {
            Assert.AreNotEqual(0ul, ownerPeerId, "Network session owner PeerId 0 is invalid.");

            foreach (var pair in _objects) {
                var networkObject = pair.Value;
                if (!networkObject.PersistOnOwnerLeave || networkObject.OwnerPeerId != previousOwnerPeerId) {
                    continue;
                }

                TransferOwner(networkObject, ownerPeerId);
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
                WriteSpawn(ref writer, networkObject.Id, networkObject);
                _messageManager.SendToPeer(peerId, writer.Written, NetworkDelivery.Reliable);
            }
        }

        private void OnMemberLeft(ulong peerId) {
            var sessionOwnerPeerId = _session.OwnerPeerId;
            var ids = new List<uint>();
            foreach (var pair in _objects) {
                var networkObject = pair.Value;
                if (networkObject.OwnerPeerId != peerId) {
                    continue;
                }

                if (networkObject.PersistOnOwnerLeave) {
                    if (sessionOwnerPeerId != 0 && sessionOwnerPeerId != peerId) {
                        TransferOwner(networkObject, sessionOwnerPeerId);
                    }

                    continue;
                }

                ids.Add(pair.Key);
            }

            foreach (var id in ids) {
                DestroyLocal(id);
            }
        }

        private void OnSpawnMessage(ulong senderPeerId, BufferReader reader) {
            var id = reader.ReadUInt();
            var prefabId = reader.ReadInt();
            var persistOnOwnerLeave = reader.ReadBool();

            if (_objects.TryGetValue(id, out var existing)) {
                Assert.AreEqual(senderPeerId, existing.OwnerPeerId, "Network object id collision between different owners.");
                Assert.AreEqual(prefabId, existing.PrefabId, "Spawn snapshot prefab does not match the existing object.");
                existing.PersistOnOwnerLeave = persistOnOwnerLeave;
                existing.DeserializeSnapshot(ref reader);
                return;
            }

            _prefabs.TryGetValue(prefabId, out var prefab);
            Assert.IsNotNull(prefab, $"Prefab id {prefabId} is not registered.");

            var instance = _factory.Instantiate(prefab);
            instance.PersistOnOwnerLeave = persistOnOwnerLeave;
            instance.DeserializeSnapshot(ref reader);
            SpawnLocal(id, senderPeerId, instance);
        }

        private void OnDespawnMessage(ulong senderPeerId, BufferReader reader) {
            var id = reader.ReadUInt();
            if (!_objects.TryGetValue(id, out var networkObject)) {
                return;
            }

            Assert.AreEqual(senderPeerId, networkObject.OwnerPeerId, "Only the current owner can despawn a network object.");
            DestroyLocal(id);
        }

        private void WriteSpawn(ref BufferWriter writer, uint id, NetworkObject networkObject) {
            writer.WriteByte(NetworkMessageType.Spawn);
            writer.WriteUInt(id);
            writer.WriteInt(networkObject.PrefabId);
            writer.WriteBool(networkObject.PersistOnOwnerLeave);
            networkObject.SerializeSnapshot(ref writer);
        }

        private void TransferOwner(NetworkObject networkObject, ulong ownerPeerId) {
            if (networkObject.OwnerPeerId == ownerPeerId) {
                return;
            }

            networkObject.SetOwner(ownerPeerId);
            OwnerChanged?.Invoke(networkObject);
        }

        private void SpawnLocal(uint id, ulong ownerPeerId, NetworkObject instance) {
            Assert.AreNotEqual(0u, id, "Network object id 0 is reserved.");
            Assert.AreNotEqual(0ul, ownerPeerId, "Network object owner PeerId 0 is invalid.");
            Assert.IsFalse(_objects.ContainsKey(id), "Network object id is already in use.");
            instance.Bind(this, id, ownerPeerId);
            _objects[id] = instance;
            Spawned?.Invoke(instance);
        }

        private void DestroyLocal(uint id) {
            if (!_objects.Remove(id, out var networkObject)) {
                return;
            }

            Despawned?.Invoke(networkObject);
            networkObject.Unbind();
            _factory.Release(networkObject);
        }
    }
}
