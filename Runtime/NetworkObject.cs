using UnityEngine;
using UnityEngine.Assertions;
using Xoderony.Networking.Serialization;

namespace Xoderony.Networking {
    /// <summary>
    /// GameObject 上的网络身份：Id 在逻辑会话内稳定且唯一，OwnerPeerId 表示当前权威端。
    /// 契约见 <see cref="INetworkObject"/>。
    /// 入网快照扩展覆写 <see cref="OnSerializeSnapshot"/> / <see cref="OnDeserializeSnapshot"/>，只走 Spawn 与晚加入。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour {
        private NetworkObjectManager _objectManager;
        [SerializeField] private int _prefabId;
        [SerializeField] private bool _persistOnOwnerLeave;

        public uint Id { get; internal set; }

        public ulong OwnerPeerId { get; internal set; }

        public bool IsSpawned => _objectManager != null;

        public bool IsOwner => IsSpawned && OwnerPeerId == _objectManager.LocalPeerId;

        public int PrefabId {
            get => _prefabId;
            internal set => _prefabId = value;
        }

        /// <summary>
        /// Owner 离开后是否保留对象并把权威交给当前会话房主。
        /// 掉落物等为 true；玩家角色等为 false，随 Owner 离开销毁。
        /// </summary>
        public bool PersistOnOwnerLeave {
            get => _persistOnOwnerLeave;
            set => _persistOnOwnerLeave = value;
        }

        /// <summary>
        /// 入网快照附加数据。仅 Spawn 与晚加入调用；派生类型须成对读写相同字节数。
        /// </summary>
        protected virtual void OnSerializeSnapshot(ref BufferWriter writer) {
        }

        /// <summary>对应 <see cref="OnSerializeSnapshot"/>。</summary>
        protected virtual void OnDeserializeSnapshot(ref BufferReader reader) {
        }

        internal void Bind(NetworkObjectManager objectManager, uint id, ulong ownerPeerId) {
            _objectManager = objectManager;
            Id = id;
            OwnerPeerId = ownerPeerId;
        }

        internal void SetOwner(ulong ownerPeerId) {
            Assert.IsTrue(IsSpawned, "Instance is not spawned.");
            Assert.AreNotEqual(0ul, ownerPeerId, "Network object owner PeerId 0 is invalid.");
            OwnerPeerId = ownerPeerId;
        }

        internal void Unbind() {
            Assert.IsTrue(IsSpawned, "Instance is not spawned.");

            _objectManager = null;
            Id = default;
            OwnerPeerId = default;
        }

        internal void SerializeSnapshot(ref BufferWriter writer) {
            OnSerializeSnapshot(ref writer);
        }

        internal void DeserializeSnapshot(ref BufferReader reader) {
            OnDeserializeSnapshot(ref reader);
        }
    }
}
